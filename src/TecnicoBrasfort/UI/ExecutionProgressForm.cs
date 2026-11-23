using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.UI
{
    public class ExecutionProgressForm : Form, IExecutionObserver
    {
        private static readonly Color ColorDarkSlate = Color.FromArgb(48, 56, 65);      // #303841
        private static readonly Color ColorProgressTrack = Color.FromArgb(32, 38, 45);  // #20262D
        private static readonly Color ColorWarmBg = Color.FromArgb(239, 232, 221);      // #EFE8DD
        private static readonly Color ColorTealAccent = Color.FromArgb(118, 171, 174);  // #76ABAE
        private static readonly Color ColorOrangeCta = Color.FromArgb(255, 87, 34);     // #FF5722
        private static readonly Color ColorBorder = Color.FromArgb(212, 204, 192);      // #D4CCC0

        private readonly BatchExecutionEngine _engine;
        private readonly IList<ActionDefinition> _actions;
        private readonly Form _ownerForm;
        private readonly CancellationTokenSource _cts;
        private readonly System.Windows.Forms.Timer _elapsedTimer;
        private readonly Stopwatch _stopwatch;

        private readonly Label _lblStep;
        private readonly Label _lblElapsed;
        private readonly Panel _pnlProgressTrack;
        private readonly Panel _pnlProgressFill;
        private readonly Label _lblProgressPct;
        private readonly TextBox _txtLog;
        private readonly Panel _pnlReboot;
        private readonly Label _lblReboot;
        private readonly TextBox _txtSummary;
        private readonly Button _btnMinimizeBg;
        private readonly Button _btnCancel;
        private readonly Button _btnCopySummary;
        private readonly Button _btnOpenLogs;
        private readonly Button _btnClose;

        private BackgroundStatusBannerForm _bannerForm;
        private ActionDefinition _currentAction;
        private int _progressPct;
        private bool _isRunning;
        private bool _isBackgroundMode;

        public event EventHandler ExecutionFinished;

        public ExecutionProgressForm(
            BatchExecutionEngine engine,
            IList<ActionDefinition> actions,
            Form ownerForm)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            if (actions == null) throw new ArgumentNullException("actions");

            _engine = engine;
            _actions = actions;
            _ownerForm = ownerForm;
            _cts = new CancellationTokenSource();
            _stopwatch = new Stopwatch();
            _progressPct = 0;
            _isRunning = false;
            _isBackgroundMode = false;

            Text = "Tecnico Brasfort - Execucao em Andamento";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(720, 560);
            ClientSize = new Size(800, 610);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            BackColor = ColorWarmBg;
            Icon = SystemIcons.Shield;

            // Cabecalho escuro (#303841)
            Panel pnlHeader = new Panel();
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Size = new Size(800, 82);
            pnlHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlHeader.BackColor = ColorDarkSlate;
            Controls.Add(pnlHeader);

            _lblStep = new Label();
            _lblStep.Text = "Preparando execucao...";
            _lblStep.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _lblStep.ForeColor = Color.White;
            _lblStep.Location = new Point(16, 12);
            _lblStep.Size = new Size(610, 26);
            _lblStep.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlHeader.Controls.Add(_lblStep);

            _lblElapsed = new Label();
            _lblElapsed.Text = "Tempo: 00:00";
            _lblElapsed.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _lblElapsed.BackColor = ColorTealAccent;
            _lblElapsed.ForeColor = ColorDarkSlate;
            _lblElapsed.TextAlign = ContentAlignment.MiddleCenter;
            _lblElapsed.Location = new Point(644, 12);
            _lblElapsed.Size = new Size(140, 26);
            _lblElapsed.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlHeader.Controls.Add(_lblElapsed);

            _pnlProgressTrack = new Panel();
            _pnlProgressTrack.Location = new Point(16, 48);
            _pnlProgressTrack.Size = new Size(768, 20);
            _pnlProgressTrack.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _pnlProgressTrack.BackColor = ColorProgressTrack;
            _pnlProgressTrack.SizeChanged += delegate(object s, EventArgs e) { UpdateProgressVisual(); };
            pnlHeader.Controls.Add(_pnlProgressTrack);

            _pnlProgressFill = new Panel();
            _pnlProgressFill.Location = new Point(0, 0);
            _pnlProgressFill.Size = new Size(0, 20);
            _pnlProgressFill.BackColor = ColorTealAccent;
            _pnlProgressTrack.Controls.Add(_pnlProgressFill);

            _lblProgressPct = new Label();
            _lblProgressPct.Text = "0%";
            _lblProgressPct.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblProgressPct.ForeColor = Color.White;
            _lblProgressPct.BackColor = Color.Transparent;
            _lblProgressPct.TextAlign = ContentAlignment.MiddleCenter;
            _lblProgressPct.Dock = DockStyle.Fill;
            _pnlProgressTrack.Controls.Add(_lblProgressPct);
            _lblProgressPct.BringToFront();

            // Saida em tempo real
            Label lblLogTitle = new Label();
            lblLogTitle.Text = "Saida em tempo real:";
            lblLogTitle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblLogTitle.ForeColor = ColorDarkSlate;
            lblLogTitle.Location = new Point(16, 92);
            lblLogTitle.Size = new Size(300, 20);
            Controls.Add(lblLogTitle);

            Panel pnlLogCard = new Panel();
            pnlLogCard.Location = new Point(16, 114);
            pnlLogCard.Size = new Size(768, 224);
            pnlLogCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlLogCard.BackColor = ColorDarkSlate;
            pnlLogCard.Padding = new Padding(8);
            Controls.Add(pnlLogCard);

            _txtLog = new TextBox();
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.BorderStyle = BorderStyle.None;
            _txtLog.ScrollBars = ScrollBars.Vertical;
            _txtLog.Font = new Font("Consolas", 9F, FontStyle.Regular);
            _txtLog.BackColor = ColorDarkSlate;
            _txtLog.ForeColor = Color.FromArgb(242, 242, 242);
            _txtLog.Dock = DockStyle.Fill;
            pnlLogCard.Controls.Add(_txtLog);

            // Faixa de reboot (exibida ao concluir)
            _pnlReboot = new Panel();
            _pnlReboot.Location = new Point(16, 346);
            _pnlReboot.Size = new Size(768, 32);
            _pnlReboot.BackColor = Color.White;
            _pnlReboot.BorderStyle = BorderStyle.FixedSingle;
            _pnlReboot.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _pnlReboot.Visible = false;
            Controls.Add(_pnlReboot);

            _lblReboot = new Label();
            _lblReboot.Dock = DockStyle.Fill;
            _lblReboot.TextAlign = ContentAlignment.MiddleLeft;
            _lblReboot.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _lblReboot.Padding = new Padding(8, 0, 0, 0);
            _pnlReboot.Controls.Add(_lblReboot);

            // Card de Fechamento de Chamado
            Label lblSummaryTitle = new Label();
            lblSummaryTitle.Text = "Fechamento de Chamado:";
            lblSummaryTitle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblSummaryTitle.ForeColor = ColorDarkSlate;
            lblSummaryTitle.Location = new Point(16, 384);
            lblSummaryTitle.Size = new Size(300, 20);
            lblSummaryTitle.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(lblSummaryTitle);

            Panel pnlSummaryCard = new Panel();
            pnlSummaryCard.Location = new Point(16, 406);
            pnlSummaryCard.Size = new Size(768, 140);
            pnlSummaryCard.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlSummaryCard.BackColor = Color.White;
            pnlSummaryCard.BorderStyle = BorderStyle.FixedSingle;
            pnlSummaryCard.Padding = new Padding(8);
            Controls.Add(pnlSummaryCard);

            _txtSummary = new TextBox();
            _txtSummary.Multiline = true;
            _txtSummary.ReadOnly = true;
            _txtSummary.BorderStyle = BorderStyle.None;
            _txtSummary.ScrollBars = ScrollBars.Vertical;
            _txtSummary.Font = new Font("Consolas", 9F, FontStyle.Regular);
            _txtSummary.BackColor = Color.White;
            _txtSummary.ForeColor = ColorDarkSlate;
            _txtSummary.Dock = DockStyle.Fill;
            pnlSummaryCard.Controls.Add(_txtSummary);

            // Barra inferior organizada
            _btnMinimizeBg = new Button();
            _btnMinimizeBg.Text = "Minimizar para Segundo Plano";
            _btnMinimizeBg.Location = new Point(16, 560);
            _btnMinimizeBg.Size = new Size(212, 34);
            _btnMinimizeBg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnMinimizeBg.FlatStyle = FlatStyle.Flat;
            _btnMinimizeBg.BackColor = Color.White;
            _btnMinimizeBg.ForeColor = ColorDarkSlate;
            _btnMinimizeBg.FlatAppearance.BorderColor = ColorBorder;
            _btnMinimizeBg.Cursor = Cursors.Hand;
            _btnMinimizeBg.Click += OnMinimizeBackgroundClick;
            Controls.Add(_btnMinimizeBg);

            _btnCancel = new Button();
            _btnCancel.Text = "Cancelar";
            _btnCancel.Location = new Point(236, 560);
            _btnCancel.Size = new Size(100, 34);
            _btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.BackColor = ColorOrangeCta;
            _btnCancel.ForeColor = Color.White;
            _btnCancel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Cursor = Cursors.Hand;
            _btnCancel.Click += OnCancelClick;
            Controls.Add(_btnCancel);

            _btnCopySummary = new Button();
            _btnCopySummary.Text = "Copiar fechamento";
            _btnCopySummary.Location = new Point(394, 560);
            _btnCopySummary.Size = new Size(150, 34);
            _btnCopySummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnCopySummary.FlatStyle = FlatStyle.Flat;
            _btnCopySummary.BackColor = ColorTealAccent;
            _btnCopySummary.ForeColor = Color.White;
            _btnCopySummary.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnCopySummary.FlatAppearance.BorderSize = 0;
            _btnCopySummary.Cursor = Cursors.Hand;
            _btnCopySummary.Enabled = false;
            _btnCopySummary.Click += OnCopySummaryClick;
            Controls.Add(_btnCopySummary);

            _btnOpenLogs = new Button();
            _btnOpenLogs.Text = "Abrir pasta de logs";
            _btnOpenLogs.Location = new Point(552, 560);
            _btnOpenLogs.Size = new Size(140, 34);
            _btnOpenLogs.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnOpenLogs.FlatStyle = FlatStyle.Flat;
            _btnOpenLogs.BackColor = Color.White;
            _btnOpenLogs.ForeColor = ColorDarkSlate;
            _btnOpenLogs.FlatAppearance.BorderColor = ColorBorder;
            _btnOpenLogs.Cursor = Cursors.Hand;
            _btnOpenLogs.Enabled = false;
            _btnOpenLogs.Click += OnOpenLogsClick;
            Controls.Add(_btnOpenLogs);

            _btnClose = new Button();
            _btnClose.Text = "Fechar";
            _btnClose.Location = new Point(700, 560);
            _btnClose.Size = new Size(84, 34);
            _btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.BackColor = Color.FromArgb(220, 215, 207);
            _btnClose.ForeColor = ColorDarkSlate;
            _btnClose.FlatAppearance.BorderColor = ColorBorder;
            _btnClose.Cursor = Cursors.Hand;
            _btnClose.Enabled = false;
            _btnClose.Click += delegate(object sender, EventArgs e) { Close(); };
            Controls.Add(_btnClose);

            _elapsedTimer = new System.Windows.Forms.Timer();
            _elapsedTimer.Interval = 1000;
            _elapsedTimer.Tick += delegate(object sender, EventArgs e)
            {
                TimeSpan el = _stopwatch.Elapsed;
                _lblElapsed.Text = string.Format("Tempo: {0:00}:{1:00}", (int)el.TotalMinutes, el.Seconds);
            };
        }

        public void StartExecution()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _stopwatch.Restart();
            _elapsedTimer.Start();

            Thread worker = new Thread(RunBatchWorker);
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunBatchWorker()
        {
            BatchExecutionReport report = null;
            try
            {
                report = _engine.Execute(_actions, this, _cts.Token);
            }
            catch (Exception ex)
            {
                OnError("[ERRO FATAL] " + ex.Message);
            }

            SafeBeginInvoke(delegate()
            {
                OnBatchCompleted(report);
            });
        }

        public void OnOutput(string line)
        {
            SafeBeginInvoke(delegate()
            {
                AppendLogLine(line);
            });
        }

        public void OnError(string line)
        {
            SafeBeginInvoke(delegate()
            {
                AppendLogLine(line);
            });
        }

        public void OnStepStarted(int stepIndex, int totalSteps, ActionDefinition action)
        {
            SafeBeginInvoke(delegate()
            {
                _currentAction = action;
                string name = action != null ? action.Name : "";
                _lblStep.Text = string.Format("Etapa {0} de {1}: {2}", stepIndex, totalSteps, name);
                SetProgressPercentage(totalSteps > 0 ? (int)((stepIndex - 1) * 100.0 / totalSteps) : 0);
                AppendLogLine(string.Format("=== [{0}/{1}] {2} ===", stepIndex, totalSteps, name));

                if (_bannerForm != null && !_bannerForm.IsDisposed)
                {
                    _bannerForm.UpdateProgress(stepIndex, totalSteps, name);
                }
            });
        }

        public void OnStepCompleted(int stepIndex, int totalSteps, ActionExecutionResult result)
        {
            SafeBeginInvoke(delegate()
            {
                SetProgressPercentage(totalSteps > 0 ? (int)(stepIndex * 100.0 / totalSteps) : 100);
                if (result != null)
                {
                    string status = result.Cancelled ? "CANCELADO" : (result.Success ? "OK" : "FALHA");
                    AppendLogLine(string.Format("--- [{0}] {1} ({2:0.0}s) ---", status, result.ActionName, result.Duration.TotalSeconds));
                }
            });
        }

        private void OnBatchCompleted(BatchExecutionReport report)
        {
            _isRunning = false;
            _stopwatch.Stop();
            _elapsedTimer.Stop();

            SetProgressPercentage(100);
            _lblStep.Text = "Execucao finalizada.";
            _btnMinimizeBg.Enabled = false;
            _btnCancel.Enabled = false;
            _btnCopySummary.Enabled = true;
            _btnOpenLogs.Enabled = true;
            _btnClose.Enabled = true;
            _btnClose.BackColor = Color.White;

            bool rebootRequired = report != null && report.RebootRequired;
            _pnlReboot.Visible = true;
            if (rebootRequired)
            {
                _pnlReboot.BackColor = Color.MistyRose;
                _lblReboot.ForeColor = Color.Firebrick;
                _lblReboot.Text = "REINICIALIZACAO NECESSARIA para concluir as alteracoes aplicadas.";
            }
            else
            {
                _pnlReboot.BackColor = Color.Honeydew;
                _lblReboot.ForeColor = Color.DarkGreen;
                _lblReboot.Text = "Nenhuma reinicializacao pendente detectada.";
            }

            if (report != null)
            {
                _txtSummary.Text = report.TicketSummaryText;
                if (!string.IsNullOrEmpty(report.SavedSummaryPath))
                {
                    AppendLogLine("Fechamento salvo em: " + report.SavedSummaryPath);
                }
            }

            if (_isBackgroundMode)
            {
                RestoreFromBackground();
            }

            EventHandler handler = ExecutionFinished;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void SetProgressPercentage(int pct)
        {
            _progressPct = Math.Max(0, Math.Min(100, pct));
            UpdateProgressVisual();
        }

        private void UpdateProgressVisual()
        {
            if (_pnlProgressTrack == null || _pnlProgressFill == null || _lblProgressPct == null)
            {
                return;
            }
            int fillWidth = (int)Math.Round(_pnlProgressTrack.ClientSize.Width * (_progressPct / 100.0));
            _pnlProgressFill.Size = new Size(fillWidth, _pnlProgressTrack.ClientSize.Height);
            _lblProgressPct.Text = string.Format("{0}%", _progressPct);
        }

        private void OnMinimizeBackgroundClick(object sender, EventArgs e)
        {
            if (!_isRunning)
            {
                return;
            }

            _isBackgroundMode = true;
            if (_bannerForm == null || _bannerForm.IsDisposed)
            {
                _bannerForm = new BackgroundStatusBannerForm();
                _bannerForm.ExpandRequested += delegate(object s, EventArgs args)
                {
                    RestoreFromBackground();
                };
            }

            Hide();
            if (_ownerForm != null && !_ownerForm.IsDisposed)
            {
                _ownerForm.Hide();
            }
            _bannerForm.Show();
        }

        private void RestoreFromBackground()
        {
            _isBackgroundMode = false;
            if (_bannerForm != null && !_bannerForm.IsDisposed)
            {
                _bannerForm.Close();
                _bannerForm.Dispose();
                _bannerForm = null;
            }

            if (_ownerForm != null && !_ownerForm.IsDisposed)
            {
                _ownerForm.Show();
            }
            Show();
            BringToFront();
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            if (!_isRunning)
            {
                return;
            }

            string extra = "";
            if (_currentAction != null &&
                (_currentAction.Id == "maint_dism" || _currentAction.Id == "maint_sfc"))
            {
                extra = "\n\nATENCAO: Cancelar DISM ou SFC no meio pode deixar operacoes pendentes no sistema.";
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "Deseja realmente cancelar a fila em andamento?" + extra,
                "Confirmar Cancelamento",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                _btnCancel.Enabled = false;
                _lblStep.Text = "Cancelando operacao atual...";
                AppendLogLine("[AVISO] Cancelamento solicitado pelo tecnico...");
                _cts.Cancel();
            }
        }

        private void OnCopySummaryClick(object sender, EventArgs e)
        {
            if (ActionHelpDialog.TryCopyToClipboard(_txtSummary.Text))
            {
                MessageBox.Show(this, "Resumo de fechamento copiado.", "Copiado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "Nao foi possivel copiar para a area de transferencia.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnOpenLogsClick(object sender, EventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", AuditLogger.DefaultPrimaryDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Erro ao abrir pasta de logs: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isRunning && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                OnMinimizeBackgroundClick(this, EventArgs.Empty);
                return;
            }

            if (_bannerForm != null && !_bannerForm.IsDisposed)
            {
                _bannerForm.Close();
                _bannerForm.Dispose();
                _bannerForm = null;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Dispose();
                _elapsedTimer.Dispose();
                if (_bannerForm != null && !_bannerForm.IsDisposed)
                {
                    _bannerForm.Dispose();
                    _bannerForm = null;
                }
            }
            base.Dispose(disposing);
        }

        private void SafeBeginInvoke(MethodInvoker action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void AppendLogLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            _txtLog.AppendText(line + Environment.NewLine);
        }
    }
}
