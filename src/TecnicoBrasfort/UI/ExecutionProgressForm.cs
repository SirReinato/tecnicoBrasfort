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
        private readonly BatchExecutionEngine _engine;
        private readonly IList<ActionDefinition> _actions;
        private readonly Form _ownerForm;
        private readonly CancellationTokenSource _cts;
        private readonly System.Windows.Forms.Timer _elapsedTimer;
        private readonly Stopwatch _stopwatch;

        private readonly Label _lblStep;
        private readonly Label _lblElapsed;
        private readonly ProgressBar _progressBar;
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
            _isRunning = false;
            _isBackgroundMode = false;

            Text = "Tecnico Brasfort - Execucao em Andamento";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(680, 520);
            ClientSize = new Size(760, 580);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            Icon = SystemIcons.Shield;

            _lblStep = new Label();
            _lblStep.Text = "Preparando execucao...";
            _lblStep.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _lblStep.Location = new Point(16, 14);
            _lblStep.Size = new Size(560, 24);
            _lblStep.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_lblStep);

            _lblElapsed = new Label();
            _lblElapsed.Text = "Tempo: 00:00";
            _lblElapsed.TextAlign = ContentAlignment.MiddleRight;
            _lblElapsed.Location = new Point(590, 14);
            _lblElapsed.Size = new Size(154, 24);
            _lblElapsed.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(_lblElapsed);

            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(16, 44);
            _progressBar.Size = new Size(728, 20);
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            _progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_progressBar);

            Label lblLogTitle = new Label();
            lblLogTitle.Text = "Saida em tempo real:";
            lblLogTitle.Location = new Point(16, 72);
            lblLogTitle.Size = new Size(300, 20);
            Controls.Add(lblLogTitle);

            _txtLog = new TextBox();
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Vertical;
            _txtLog.Font = new Font("Consolas", 9F, FontStyle.Regular);
            _txtLog.BackColor = Color.FromArgb(24, 24, 24);
            _txtLog.ForeColor = Color.Gainsboro;
            _txtLog.Location = new Point(16, 94);
            _txtLog.Size = new Size(728, 220);
            _txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_txtLog);

            _pnlReboot = new Panel();
            _pnlReboot.Location = new Point(16, 322);
            _pnlReboot.Size = new Size(728, 34);
            _pnlReboot.BackColor = Color.WhiteSmoke;
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

            Label lblSummaryTitle = new Label();
            lblSummaryTitle.Text = "Fechamento de Chamado:";
            lblSummaryTitle.Location = new Point(16, 364);
            lblSummaryTitle.Size = new Size(300, 20);
            lblSummaryTitle.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(lblSummaryTitle);

            _txtSummary = new TextBox();
            _txtSummary.Multiline = true;
            _txtSummary.ReadOnly = true;
            _txtSummary.ScrollBars = ScrollBars.Vertical;
            _txtSummary.Font = new Font("Consolas", 9F, FontStyle.Regular);
            _txtSummary.BackColor = Color.WhiteSmoke;
            _txtSummary.Location = new Point(16, 386);
            _txtSummary.Size = new Size(728, 136);
            _txtSummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_txtSummary);

            _btnMinimizeBg = new Button();
            _btnMinimizeBg.Text = "Minimizar para Segundo Plano";
            _btnMinimizeBg.Location = new Point(16, 534);
            _btnMinimizeBg.Size = new Size(210, 32);
            _btnMinimizeBg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnMinimizeBg.Click += OnMinimizeBackgroundClick;
            Controls.Add(_btnMinimizeBg);

            _btnCancel = new Button();
            _btnCancel.Text = "Cancelar";
            _btnCancel.Location = new Point(236, 534);
            _btnCancel.Size = new Size(96, 32);
            _btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnCancel.Click += OnCancelClick;
            Controls.Add(_btnCancel);

            _btnCopySummary = new Button();
            _btnCopySummary.Text = "Copiar fechamento";
            _btnCopySummary.Location = new Point(368, 534);
            _btnCopySummary.Size = new Size(146, 32);
            _btnCopySummary.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnCopySummary.Enabled = false;
            _btnCopySummary.Click += OnCopySummaryClick;
            Controls.Add(_btnCopySummary);

            _btnOpenLogs = new Button();
            _btnOpenLogs.Text = "Abrir pasta de logs";
            _btnOpenLogs.Location = new Point(522, 534);
            _btnOpenLogs.Size = new Size(134, 32);
            _btnOpenLogs.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnOpenLogs.Enabled = false;
            _btnOpenLogs.Click += OnOpenLogsClick;
            Controls.Add(_btnOpenLogs);

            _btnClose = new Button();
            _btnClose.Text = "Fechar";
            _btnClose.Location = new Point(664, 534);
            _btnClose.Size = new Size(80, 32);
            _btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
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
                int pct = totalSteps > 0 ? (int)((stepIndex - 1) * 100.0 / totalSteps) : 0;
                _progressBar.Value = Math.Max(0, Math.Min(100, pct));
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
                int pct = totalSteps > 0 ? (int)(stepIndex * 100.0 / totalSteps) : 100;
                _progressBar.Value = Math.Max(0, Math.Min(100, pct));
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

            _progressBar.Value = 100;
            _lblStep.Text = "Execucao finalizada.";
            _btnMinimizeBg.Enabled = false;
            _btnCancel.Enabled = false;
            _btnCopySummary.Enabled = true;
            _btnOpenLogs.Enabled = true;
            _btnClose.Enabled = true;

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
