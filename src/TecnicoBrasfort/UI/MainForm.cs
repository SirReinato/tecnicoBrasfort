using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TecnicoBrasfort.Core;
using TecnicoBrasfort.Modules;

namespace TecnicoBrasfort.UI
{
    public class MainForm : Form
    {
        private readonly EnvironmentInfo _env;
        private readonly BatchExecutionEngine _engine;
        private readonly NetworkModule _network;
        private readonly DiagnosticsModule _diagnostics;
        private readonly DriversUpdatesModule _driversUpdates;
        private readonly AuditModule _audit;

        private readonly Dictionary<string, CheckBox> _actionCheckBoxes;
        private readonly Dictionary<string, ActionDefinition> _actionsById;
        private readonly Label _lblSelectionCount;
        private readonly Button _btnExecute;
        private readonly Button _btnBackupDrivers;

        private ExecutionProgressForm _activeProgressForm;

        public MainForm(
            EnvironmentInfo env,
            BatchExecutionEngine engine,
            NetworkModule network,
            DiagnosticsModule diagnostics,
            DriversUpdatesModule driversUpdates,
            AuditModule audit)
        {
            if (env == null) throw new ArgumentNullException("env");
            if (engine == null) throw new ArgumentNullException("engine");

            _env = env;
            _engine = engine;
            _network = network;
            _diagnostics = diagnostics;
            _driversUpdates = driversUpdates;
            _audit = audit;

            _actionCheckBoxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
            _actionsById = new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase);

            Text = "Tecnico Brasfort v2 - Painel de Suporte TI";
            MinimumSize = new Size(920, 660);
            ClientSize = new Size(980, 700);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            Icon = SystemIcons.Shield;

            // Zona 1: Cabecalho de Contexto
            Panel pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = _env.IsUncOrigin ? 76 : 54;
            pnlHeader.BackColor = Color.FromArgb(240, 244, 248);
            pnlHeader.Padding = new Padding(12, 8, 12, 8);
            Controls.Add(pnlHeader);

            Label lblContext = new Label();
            lblContext.Text = string.Format(
                "PC: {0}  |  SO: {1}  |  Admin: {2}  |  Usuario: {3}",
                _env.Hostname,
                _env.OsVersion,
                _env.ElevatedAdmin,
                string.IsNullOrEmpty(_env.InteractiveUser) ? "(nenhum)" : _env.InteractiveUser);
            lblContext.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblContext.Location = new Point(12, 10);
            lblContext.Size = new Size(940, 22);
            lblContext.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlHeader.Controls.Add(lblContext);

            if (_env.IsUncOrigin)
            {
                Label lblUnc = new Label();
                lblUnc.Text = "Origem: Rede (UNC) — " + _env.OriginDirectory;
                lblUnc.ForeColor = Color.DarkSlateBlue;
                lblUnc.Location = new Point(12, 38);
                lblUnc.Size = new Size(940, 20);
                lblUnc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                pnlHeader.Controls.Add(lblUnc);
            }

            // Zona 2: Barra dos 5 Combos + Limpar selecao
            Panel pnlCombos = new Panel();
            pnlCombos.Dock = DockStyle.Top;
            pnlCombos.Height = 84;
            pnlCombos.BackColor = Color.White;
            pnlCombos.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(pnlCombos);

            Label lblCombosTitle = new Label();
            lblCombosTitle.Text = "Combos rapidos (marcam automaticamente as acoes recomendadas):";
            lblCombosTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCombosTitle.Location = new Point(12, 8);
            lblCombosTitle.Size = new Size(500, 18);
            pnlCombos.Controls.Add(lblCombosTitle);

            FlowLayoutPanel flowCombos = new FlowLayoutPanel();
            flowCombos.Location = new Point(10, 30);
            flowCombos.Size = new Size(956, 46);
            flowCombos.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            flowCombos.WrapContents = true;
            pnlCombos.Controls.Add(flowCombos);

            IList<ComboDefinition> combos = ActionCatalog.GetCombos();
            for (int i = 0; i < combos.Count; i++)
            {
                ComboDefinition combo = combos[i];
                Button btnCombo = new Button();
                btnCombo.Text = combo.Name;
                btnCombo.AutoSize = true;
                btnCombo.Height = 30;
                btnCombo.Padding = new Padding(6, 2, 6, 2);
                btnCombo.Margin = new Padding(3);
                btnCombo.Tag = combo;
                btnCombo.Click += OnComboButtonClick;
                flowCombos.Controls.Add(btnCombo);
            }

            Button btnClear = new Button();
            btnClear.Text = "Limpar selecao";
            btnClear.AutoSize = true;
            btnClear.Height = 30;
            btnClear.Padding = new Padding(6, 2, 6, 2);
            btnClear.Margin = new Padding(10, 3, 3, 3);
            btnClear.Click += OnClearSelectionClick;
            flowCombos.Controls.Add(btnClear);

            // Rodape
            Panel pnlFooter = new Panel();
            pnlFooter.Dock = DockStyle.Bottom;
            pnlFooter.Height = 56;
            pnlFooter.BackColor = Color.FromArgb(245, 245, 245);
            pnlFooter.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(pnlFooter);

            _lblSelectionCount = new Label();
            _lblSelectionCount.Text = "0 acao(oes) selecionada(s)";
            _lblSelectionCount.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _lblSelectionCount.Location = new Point(16, 17);
            _lblSelectionCount.Size = new Size(340, 22);
            pnlFooter.Controls.Add(_lblSelectionCount);

            _btnExecute = new Button();
            _btnExecute.Text = "Executar Selecionadas";
            _btnExecute.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnExecute.BackColor = Color.FromArgb(0, 102, 204);
            _btnExecute.ForeColor = Color.White;
            _btnExecute.FlatStyle = FlatStyle.Flat;
            _btnExecute.Location = new Point(740, 10);
            _btnExecute.Size = new Size(220, 34);
            _btnExecute.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnExecute.Click += OnExecuteSelectedClick;
            pnlFooter.Controls.Add(_btnExecute);

            // Zona 3: Painel principal (Acoes em lote a esquerda + Ferramentas Rapidas a direita)
            Panel pnlQuickTools = new Panel();
            pnlQuickTools.Dock = DockStyle.Right;
            pnlQuickTools.Width = 250;
            pnlQuickTools.BackColor = Color.FromArgb(250, 250, 252);
            pnlQuickTools.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(pnlQuickTools);

            Label lblQuickTitle = new Label();
            lblQuickTitle.Text = "Ferramentas Rapidas";
            lblQuickTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblQuickTitle.Location = new Point(12, 12);
            lblQuickTitle.Size = new Size(224, 22);
            pnlQuickTools.Controls.Add(lblQuickTitle);

            Label lblQuickSub = new Label();
            lblQuickSub.Text = "Executam imediatamente fora da fila:";
            lblQuickSub.ForeColor = Color.DimGray;
            lblQuickSub.Location = new Point(12, 36);
            lblQuickSub.Size = new Size(224, 20);
            pnlQuickTools.Controls.Add(lblQuickSub);

            int quickY = 68;
            AddQuickToolButton(pnlQuickTools, "Abrir Conexoes de Rede", ref quickY, delegate(object s, EventArgs e)
            {
                _network.OpenNetworkConnections();
            });
            AddQuickToolButton(pnlQuickTools, "Monitor de Recursos (resmon)", ref quickY, delegate(object s, EventArgs e)
            {
                _diagnostics.ShowProcessMonitor(true);
            });
            _btnBackupDrivers = AddQuickToolButton(pnlQuickTools, "Backup dos Drivers (pnputil)", ref quickY, OnBackupDriversClick);
            AddQuickToolButton(pnlQuickTools, "Configuracoes Windows Update", ref quickY, delegate(object s, EventArgs e)
            {
                _driversUpdates.OpenWindowsUpdateSettings();
            });
            AddQuickToolButton(pnlQuickTools, "Ver Logs Locais deste PC", ref quickY, OnViewLogsClick);

            Panel pnlActionsContainer = new Panel();
            pnlActionsContainer.Dock = DockStyle.Fill;
            pnlActionsContainer.AutoScroll = true;
            pnlActionsContainer.BackColor = Color.White;
            Controls.Add(pnlActionsContainer);

            // Ordem de Dock no WinForms exige Fill por ultimo no Z-order (BringToFront)
            pnlActionsContainer.BringToFront();

            PopulateBatchActions(pnlActionsContainer);
            UpdateSelectionCount();
        }

        private void PopulateBatchActions(Panel container)
        {
            IList<ActionDefinition> actions = ActionCatalog.GetBatchableActions();
            string currentCategory = "";
            int y = 12;

            for (int i = 0; i < actions.Count; i++)
            {
                ActionDefinition action = actions[i];
                _actionsById[action.Id] = action;

                if (!string.Equals(currentCategory, action.Category, StringComparison.OrdinalIgnoreCase))
                {
                    currentCategory = action.Category;
                    Label lblCat = new Label();
                    lblCat.Text = currentCategory.ToUpperInvariant();
                    lblCat.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    lblCat.ForeColor = Color.FromArgb(0, 78, 152);
                    lblCat.Location = new Point(14, y);
                    lblCat.Size = new Size(660, 20);
                    container.Controls.Add(lblCat);
                    y += 24;
                }

                CheckBox chk = new CheckBox();
                chk.Text = action.Name;
                chk.Location = new Point(22, y);
                chk.Size = new Size(440, 24);
                chk.Tag = action;
                chk.CheckedChanged += delegate(object s, EventArgs e)
                {
                    UpdateSelectionCount();
                };
                container.Controls.Add(chk);
                _actionCheckBoxes[action.Id] = chk;

                int badgeX = 468;
                if (action.DropsRemoteAccess)
                {
                    AddBadge(container, "Remoto!", Color.Firebrick, badgeX, y + 2);
                    badgeX += 64;
                }
                if (action.RequiresReboot)
                {
                    AddBadge(container, "Reboot", Color.DarkOrange, badgeX, y + 2);
                    badgeX += 56;
                }
                else if (action.IsDestructive)
                {
                    AddBadge(container, "Destrutiva", Color.Maroon, badgeX, y + 2);
                    badgeX += 72;
                }

                Button btnHelp = new Button();
                btnHelp.Text = "?";
                btnHelp.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                btnHelp.Location = new Point(650, y);
                btnHelp.Size = new Size(28, 23);
                btnHelp.Tag = action;
                btnHelp.Click += OnActionHelpClick;
                container.Controls.Add(btnHelp);

                y += 28;
            }
        }

        private static void AddBadge(Panel container, string text, Color color, int x, int y)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            lbl.ForeColor = Color.White;
            lbl.BackColor = color;
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.Location = new Point(x, y);
            lbl.Size = new Size(text.Length * 7 + 10, 18);
            container.Controls.Add(lbl);
        }

        private static Button AddQuickToolButton(Panel parent, string text, ref int y, EventHandler onClick)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = new Point(12, y);
            btn.Size = new Size(224, 34);
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(6, 0, 0, 0);
            btn.Click += onClick;
            parent.Controls.Add(btn);
            y += 42;
            return btn;
        }

        private void OnComboButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            ComboDefinition combo = btn != null ? btn.Tag as ComboDefinition : null;
            if (combo == null)
            {
                return;
            }

            HashSet<string> targetIds = new HashSet<string>(combo.ActionIds, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, CheckBox> kvp in _actionCheckBoxes)
            {
                kvp.Value.Checked = targetIds.Contains(kvp.Key);
            }
            UpdateSelectionCount();
        }

        private void OnClearSelectionClick(object sender, EventArgs e)
        {
            foreach (KeyValuePair<string, CheckBox> kvp in _actionCheckBoxes)
            {
                kvp.Value.Checked = false;
            }
            UpdateSelectionCount();
        }

        private void OnActionHelpClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            ActionDefinition action = btn != null ? btn.Tag as ActionDefinition : null;
            if (action == null)
            {
                return;
            }

            using (ActionHelpDialog dlg = new ActionHelpDialog(action))
            {
                dlg.ShowDialog(this);
            }
        }

        private void OnExecuteSelectedClick(object sender, EventArgs e)
        {
            if (_activeProgressForm != null && !_activeProgressForm.IsDisposed)
            {
                _activeProgressForm.Show();
                _activeProgressForm.BringToFront();
                return;
            }

            List<ActionDefinition> selected = GetSelectedActionsOrdered();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Selecione ao menos uma acao ou um combo antes de executar.", "Nenhuma Acao Selecionada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!ConfirmPreFlightIfNeeded(selected))
            {
                return;
            }

            _btnExecute.Enabled = false;
            _activeProgressForm = new ExecutionProgressForm(_engine, selected, this);
            _activeProgressForm.ExecutionFinished += delegate(object s, EventArgs args)
            {
                _btnExecute.Enabled = true;
            };
            _activeProgressForm.FormClosed += delegate(object s, FormClosedEventArgs args)
            {
                _btnExecute.Enabled = true;
                _activeProgressForm = null;
            };

            _activeProgressForm.Show(this);
            _activeProgressForm.StartExecution();
        }

        private bool ConfirmPreFlightIfNeeded(IList<ActionDefinition> selected)
        {
            List<string> warnings = new List<string>();
            bool hasRemoteDrop = false;

            for (int i = 0; i < selected.Count; i++)
            {
                ActionDefinition a = selected[i];
                if (a.DropsRemoteAccess)
                {
                    hasRemoteDrop = true;
                    warnings.Add(" - [DERRUBA ACESSO REMOTO] " + a.Name);
                }
                else if (a.RequiresReboot || a.IsDestructive)
                {
                    warnings.Add(" - " + a.Name);
                }
            }

            if (warnings.Count == 0)
            {
                return true;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("A fila contem acao(oes) de impacto ou reinicializacao:");
            sb.AppendLine();
            for (int i = 0; i < warnings.Count; i++)
            {
                sb.AppendLine(warnings[i]);
            }
            if (hasRemoteDrop)
            {
                sb.AppendLine();
                sb.AppendLine("ATENCAO: Reset de rede interrompe temporariamente AnyDesk/RDP/TeamViewer.");
            }
            sb.AppendLine();
            sb.Append("Deseja continuar?");

            DialogResult res = MessageBox.Show(
                this,
                sb.ToString(),
                "Confirmacao Pre-Voo",
                MessageBoxButtons.YesNo,
                hasRemoteDrop ? MessageBoxIcon.Warning : MessageBoxIcon.Question);

            return res == DialogResult.Yes;
        }

        private void OnBackupDriversClick(object sender, EventArgs e)
        {
            string destination = "";
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Selecione a pasta de destino para Backup de Drivers (ou Cancelar para padrao C:\\Temp):";
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    destination = fbd.SelectedPath;
                }
            }

            _btnBackupDrivers.Enabled = false;
            _btnBackupDrivers.Text = "Exportando drivers...";

            Thread worker = new Thread(delegate()
            {
                ActionExecutionResult result = _driversUpdates.BackupDrivers(destination, NullExecutionObserver.Instance, CancellationToken.None);
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new MethodInvoker(delegate()
                    {
                        _btnBackupDrivers.Enabled = true;
                        _btnBackupDrivers.Text = "Backup dos Drivers (pnputil)";
                        MessageBox.Show(
                            this,
                            result.Summary,
                            result.Success ? "Backup Concluido" : "Aviso no Backup de Drivers",
                            MessageBoxButtons.OK,
                            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    }));
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void OnViewLogsClick(object sender, EventArgs e)
        {
            IList<string> lines = _audit.GetHostLogLines();
            using (Form logForm = new Form())
            {
                logForm.Text = "Registros de Auditoria - " + _env.Hostname;
                logForm.Size = new Size(820, 520);
                logForm.StartPosition = FormStartPosition.CenterParent;
                logForm.AutoScaleMode = AutoScaleMode.Font;

                TextBox txt = new TextBox();
                txt.Multiline = true;
                txt.ReadOnly = true;
                txt.ScrollBars = ScrollBars.Both;
                txt.WordWrap = false;
                txt.Font = new Font("Consolas", 9F, FontStyle.Regular);
                txt.Dock = DockStyle.Fill;
                txt.Text = lines.Count == 0
                    ? "Nenhum registro encontrado para este computador em ProgramData."
                    : string.Join(Environment.NewLine, new List<string>(lines).ToArray());

                logForm.Controls.Add(txt);
                logForm.ShowDialog(this);
            }
        }

        private List<ActionDefinition> GetSelectedActionsOrdered()
        {
            List<ActionDefinition> selected = new List<ActionDefinition>();
            IList<ActionDefinition> catalog = ActionCatalog.GetBatchableActions();
            for (int i = 0; i < catalog.Count; i++)
            {
                ActionDefinition def = catalog[i];
                CheckBox chk;
                if (_actionCheckBoxes.TryGetValue(def.Id, out chk) && chk.Checked)
                {
                    selected.Add(def);
                }
            }
            return selected;
        }

        private void UpdateSelectionCount()
        {
            int count = 0;
            foreach (KeyValuePair<string, CheckBox> kvp in _actionCheckBoxes)
            {
                if (kvp.Value.Checked)
                {
                    count++;
                }
            }
            _lblSelectionCount.Text = string.Format("{0} acao(oes) selecionada(s)", count);
        }
    }
}
