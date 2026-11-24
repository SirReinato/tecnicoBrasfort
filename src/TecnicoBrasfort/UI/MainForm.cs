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
        private static readonly Color ColorDarkSlate = Color.FromArgb(48, 56, 65);      // #303841
        private static readonly Color ColorDarkButton = Color.FromArgb(58, 67, 77);     // #3A434D
        private static readonly Color ColorWarmBg = Color.FromArgb(239, 232, 221);      // #EFE8DD
        private static readonly Color ColorTealAccent = Color.FromArgb(118, 171, 174);  // #76ABAE
        private static readonly Color ColorOrangeCta = Color.FromArgb(255, 87, 34);     // #FF5722
        private static readonly Color ColorCardBorder = Color.FromArgb(212, 204, 192);  // #D4CCC0
        private static readonly Color ColorCatHeader = Color.FromArgb(30, 90, 145);

        private readonly EnvironmentInfo _env;
        private readonly BatchExecutionEngine _engine;
        private readonly NetworkModule _network;
        private readonly DiagnosticsModule _diagnostics;
        private readonly DriversUpdatesModule _driversUpdates;
        private readonly AuditModule _audit;
        private readonly Action<Form> _promoteProgressToPrimary;

        private readonly Dictionary<string, CheckBox> _actionCheckBoxes;
        private readonly Dictionary<string, ActionDefinition> _actionsById;
        private readonly List<Button> _comboButtons;
        private readonly List<Panel> _categoryCards;
        private readonly Panel _pnlCombos;
        private readonly FlowLayoutPanel _flowCombos;
        private readonly Panel _pnlActionsContainer;
        private readonly Label _lblSelectionCount;
        private readonly Button _btnExecute;
        private readonly Button _btnBackupDrivers;

        private string _activeComboId;
        private bool _suppressCheckChanged;
        private ExecutionProgressForm _activeProgressForm;

        public MainForm(
            EnvironmentInfo env,
            BatchExecutionEngine engine,
            NetworkModule network,
            DiagnosticsModule diagnostics,
            DriversUpdatesModule driversUpdates,
            AuditModule audit,
            Action<Form> promoteProgressToPrimary)
        {
            if (env == null) throw new ArgumentNullException("env");
            if (engine == null) throw new ArgumentNullException("engine");

            _env = env;
            _engine = engine;
            _network = network;
            _diagnostics = diagnostics;
            _driversUpdates = driversUpdates;
            _audit = audit;
            _promoteProgressToPrimary = promoteProgressToPrimary;

            _actionCheckBoxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
            _actionsById = new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase);
            _comboButtons = new List<Button>();
            _categoryCards = new List<Panel>();
            _activeComboId = "";

            Text = "Tecnico Brasfort v2 - Painel de Suporte TI";
            MinimumSize = new Size(920, 660);
            ClientSize = new Size(1060, 720);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            BackColor = ColorWarmBg;
            Icon = AppIconProvider.GetAppIcon();

            // Painel lateral direito fixo: Ferramentas Rapidas (#303841)
            Panel pnlQuickTools = new Panel();
            pnlQuickTools.Dock = DockStyle.Right;
            pnlQuickTools.Width = 256;
            pnlQuickTools.BackColor = ColorDarkSlate;
            pnlQuickTools.Padding = new Padding(14, 12, 14, 12);
            Controls.Add(pnlQuickTools);

            Label lblQuickTitle = new Label();
            lblQuickTitle.Text = "Ferramentas Rapidas";
            lblQuickTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblQuickTitle.ForeColor = Color.White;
            lblQuickTitle.Location = new Point(14, 14);
            lblQuickTitle.Size = new Size(228, 22);
            pnlQuickTools.Controls.Add(lblQuickTitle);

            Label lblQuickSub = new Label();
            lblQuickSub.Text = "Executam imediatamente fora da fila:";
            lblQuickSub.ForeColor = Color.FromArgb(205, 212, 220);
            lblQuickSub.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblQuickSub.Location = new Point(14, 38);
            lblQuickSub.Size = new Size(228, 20);
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

            // Zona 1: Barra de telemetria ancorada no topo (#303841)
            Panel pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = _env.IsUncOrigin ? 58 : 38;
            pnlHeader.BackColor = ColorDarkSlate;
            Controls.Add(pnlHeader);

            Label lblContext = new Label();
            lblContext.Text = string.Format(
                "PC: {0}  |  SO: {1}  |  Admin: {2}  |  Usuario: {3}",
                _env.Hostname,
                _env.OsVersion,
                _env.ElevatedAdmin,
                string.IsNullOrEmpty(_env.InteractiveUser) ? "(nenhum)" : _env.InteractiveUser);
            lblContext.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblContext.ForeColor = Color.White;
            lblContext.Location = new Point(14, 9);
            lblContext.Size = new Size(760, 20);
            lblContext.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlHeader.Controls.Add(lblContext);

            if (_env.IsUncOrigin)
            {
                Label lblUnc = new Label();
                lblUnc.Text = "Origem: Rede (UNC) — " + _env.OriginDirectory;
                lblUnc.ForeColor = ColorTealAccent;
                lblUnc.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
                lblUnc.Location = new Point(14, 32);
                lblUnc.Size = new Size(760, 18);
                lblUnc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                pnlHeader.Controls.Add(lblUnc);
            }

            // Zona 2: Barra de Combos Rapidos com wrap automatico sem corte vertical
            _pnlCombos = new Panel();
            _pnlCombos.Dock = DockStyle.Top;
            _pnlCombos.Height = 96;
            _pnlCombos.BackColor = ColorWarmBg;
            Controls.Add(_pnlCombos);

            Label lblCombosTitle = new Label();
            lblCombosTitle.Text = "Combos rapidos (marcam automaticamente as acoes recomendadas):";
            lblCombosTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCombosTitle.ForeColor = ColorDarkSlate;
            lblCombosTitle.Location = new Point(14, 8);
            lblCombosTitle.Size = new Size(600, 18);
            _pnlCombos.Controls.Add(lblCombosTitle);

            _flowCombos = new FlowLayoutPanel();
            _flowCombos.Location = new Point(11, 28);
            _flowCombos.Size = new Size(760, 56);
            _flowCombos.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _flowCombos.WrapContents = true;
            _flowCombos.AutoSize = false;
            _pnlCombos.Controls.Add(_flowCombos);

            IList<ComboDefinition> combos = ActionCatalog.GetCombos();
            for (int i = 0; i < combos.Count; i++)
            {
                ComboDefinition combo = combos[i];
                Button btnCombo = CreatePillButton(combo.Name, false);
                btnCombo.Tag = combo;
                btnCombo.Click += OnComboButtonClick;
                _flowCombos.Controls.Add(btnCombo);
                _comboButtons.Add(btnCombo);
            }

            Button btnClear = CreatePillButton("Limpar selecao", false);
            btnClear.Margin = new Padding(10, 3, 3, 4);
            btnClear.Click += OnClearSelectionClick;
            _flowCombos.Controls.Add(btnClear);

            // Rodape fixo (#EFE8DD) com CTA em #FF5722
            Panel pnlFooter = new Panel();
            pnlFooter.Dock = DockStyle.Bottom;
            pnlFooter.Height = 56;
            pnlFooter.BackColor = ColorWarmBg;
            Controls.Add(pnlFooter);

            Panel pnlFooterLine = new Panel();
            pnlFooterLine.Dock = DockStyle.Top;
            pnlFooterLine.Height = 1;
            pnlFooterLine.BackColor = ColorCardBorder;
            pnlFooter.Controls.Add(pnlFooterLine);

            _lblSelectionCount = new Label();
            _lblSelectionCount.Text = "0 acao(oes) selecionada(s)";
            _lblSelectionCount.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _lblSelectionCount.ForeColor = ColorDarkSlate;
            _lblSelectionCount.BackColor = Color.Transparent;
            _lblSelectionCount.AutoSize = true;
            _lblSelectionCount.Location = new Point(16, 18);
            pnlFooter.Controls.Add(_lblSelectionCount);

            _btnExecute = new Button();
            _btnExecute.Text = "Executar Selecionadas";
            _btnExecute.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnExecute.BackColor = ColorOrangeCta;
            _btnExecute.ForeColor = Color.White;
            _btnExecute.FlatStyle = FlatStyle.Flat;
            _btnExecute.FlatAppearance.BorderSize = 0;
            _btnExecute.Cursor = Cursors.Hand;
            _btnExecute.Size = new Size(220, 36);
            _btnExecute.Location = new Point(Math.Max(220, pnlFooter.ClientSize.Width - _btnExecute.Width - 16), 10);
            _btnExecute.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnExecute.Click += OnExecuteSelectedClick;
            pnlFooter.Controls.Add(_btnExecute);

            pnlFooter.SizeChanged += delegate(object s, EventArgs e)
            {
                _btnExecute.Location = new Point(Math.Max(220, pnlFooter.ClientSize.Width - _btnExecute.Width - 16), 10);
            };

            // Zona 3: Container de Cards responsivos (1 coluna no compacto / Grid 2 colunas no widescreen)
            _pnlActionsContainer = new Panel();
            _pnlActionsContainer.Dock = DockStyle.Fill;
            _pnlActionsContainer.AutoScroll = true;
            _pnlActionsContainer.BackColor = ColorWarmBg;
            Controls.Add(_pnlActionsContainer);

            // Z-order Dock: header/combos/footer/right antes, Fill com BringToFront
            pnlQuickTools.SendToBack();
            pnlHeader.SendToBack();
            _pnlActionsContainer.BringToFront();

            BuildCategoryCards();

            _pnlCombos.SizeChanged += delegate(object s, EventArgs e) { UpdateCombosBarHeight(); };
            _pnlActionsContainer.SizeChanged += delegate(object s, EventArgs e) { LayoutCategoryCards(); };
            Shown += delegate(object s, EventArgs e)
            {
                UpdateCombosBarHeight();
                LayoutCategoryCards();
            };

            UpdateCombosBarHeight();
            LayoutCategoryCards();
            UpdateSelectionCount();
        }

        private void BuildCategoryCards()
        {
            IList<ActionDefinition> actions = ActionCatalog.GetBatchableActions();
            List<string> categoryOrder = new List<string>();
            Dictionary<string, List<ActionDefinition>> grouped = new Dictionary<string, List<ActionDefinition>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < actions.Count; i++)
            {
                ActionDefinition a = actions[i];
                _actionsById[a.Id] = a;
                if (!grouped.ContainsKey(a.Category))
                {
                    grouped[a.Category] = new List<ActionDefinition>();
                    categoryOrder.Add(a.Category);
                }
                grouped[a.Category].Add(a);
            }

            for (int c = 0; c < categoryOrder.Count; c++)
            {
                string catName = categoryOrder[c];
                List<ActionDefinition> catActions = grouped[catName];

                Panel card = new Panel();
                card.BackColor = Color.White;
                card.BorderStyle = BorderStyle.FixedSingle;
                card.Padding = new Padding(12, 10, 12, 10);

                Label lblCat = new Label();
                lblCat.Text = catName.ToUpperInvariant();
                lblCat.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                lblCat.ForeColor = ColorCatHeader;
                lblCat.Location = new Point(12, 10);
                lblCat.Size = new Size(300, 20);
                card.Controls.Add(lblCat);

                int y = 34;
                for (int j = 0; j < catActions.Count; j++)
                {
                    ActionDefinition action = catActions[j];

                    Panel row = new Panel();
                    row.Location = new Point(10, y);
                    row.Size = new Size(460, 28);
                    row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    row.BackColor = Color.White;

                    Button btnHelp = new Button();
                    btnHelp.Text = "?";
                    btnHelp.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    btnHelp.FlatStyle = FlatStyle.Flat;
                    btnHelp.FlatAppearance.BorderColor = ColorCardBorder;
                    btnHelp.BackColor = Color.FromArgb(248, 248, 248);
                    btnHelp.ForeColor = ColorDarkSlate;
                    btnHelp.Size = new Size(26, 22);
                    btnHelp.Location = new Point(row.Width - 28, 3);
                    btnHelp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    btnHelp.Cursor = Cursors.Hand;
                    btnHelp.Tag = action;
                    btnHelp.Click += OnActionHelpClick;
                    row.Controls.Add(btnHelp);

                    int rightCursor = btnHelp.Left - 6;
                    if (action.RequiresReboot)
                    {
                        Label badge = CreateBadge("Reboot", Color.DarkOrange);
                        badge.Location = new Point(rightCursor - badge.Width, 5);
                        badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                        row.Controls.Add(badge);
                        rightCursor = badge.Left - 4;
                    }
                    else if (action.IsDestructive && !action.DropsRemoteAccess)
                    {
                        Label badge = CreateBadge("Destrutiva", Color.Maroon);
                        badge.Location = new Point(rightCursor - badge.Width, 5);
                        badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                        row.Controls.Add(badge);
                        rightCursor = badge.Left - 4;
                    }

                    if (action.DropsRemoteAccess)
                    {
                        Label badge = CreateBadge("Remoto!", Color.Firebrick);
                        badge.Location = new Point(rightCursor - badge.Width, 5);
                        badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                        row.Controls.Add(badge);
                        rightCursor = badge.Left - 4;
                    }

                    CheckBox chk = new CheckBox();
                    chk.Text = action.Name;
                    chk.Location = new Point(4, 2);
                    chk.Size = new Size(Math.Max(180, rightCursor - 8), 24);
                    chk.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    chk.ForeColor = ColorDarkSlate;
                    chk.Tag = action;
                    chk.CheckedChanged += OnActionCheckChanged;
                    row.Controls.Add(chk);
                    _actionCheckBoxes[action.Id] = chk;

                    card.Controls.Add(row);
                    y += 28;
                }

                card.Height = y + 10;
                _categoryCards.Add(card);
                _pnlActionsContainer.Controls.Add(card);
            }
        }

        private void LayoutCategoryCards()
        {
            if (_pnlActionsContainer == null || _categoryCards.Count == 0)
            {
                return;
            }

            int availWidth = _pnlActionsContainer.ClientSize.Width;
            if (availWidth <= 0)
            {
                return;
            }

            _pnlActionsContainer.SuspendLayout();

            const int marginX = 14;
            const int gapX = 14;
            const int gapY = 12;

            bool useTwoColumns = availWidth >= 820;
            if (useTwoColumns)
            {
                int colWidth = (availWidth - (marginX * 2) - gapX) / 2;
                int[] colY = new int[] { 8, 8 };

                for (int i = 0; i < _categoryCards.Count; i++)
                {
                    int col = colY[0] <= colY[1] ? 0 : 1;
                    Panel card = _categoryCards[i];
                    int x = marginX + (col * (colWidth + gapX));
                    card.Location = new Point(x, colY[col]);
                    card.Width = colWidth;
                    colY[col] += card.Height + gapY;
                }
            }
            else
            {
                int colWidth = Math.Max(360, availWidth - (marginX * 2));
                int y = 8;
                for (int i = 0; i < _categoryCards.Count; i++)
                {
                    Panel card = _categoryCards[i];
                    card.Location = new Point(marginX, y);
                    card.Width = colWidth;
                    y += card.Height + gapY;
                }
            }

            _pnlActionsContainer.ResumeLayout(true);
        }

        private void UpdateCombosBarHeight()
        {
            if (_pnlCombos == null || _flowCombos == null)
            {
                return;
            }

            int targetWidth = Math.Max(200, _pnlCombos.ClientSize.Width - 22);
            _flowCombos.Width = targetWidth;
            Size preferred = _flowCombos.GetPreferredSize(new Size(targetWidth, 0));
            int flowHeight = Math.Max(36, preferred.Height + 4);
            _flowCombos.Height = flowHeight;
            _pnlCombos.Height = flowHeight + 36;
        }

        private static Button CreatePillButton(string text, bool isActive)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.AutoSize = true;
            btn.MinimumSize = new Size(0, 30);
            btn.Padding = new Padding(10, 4, 10, 4);
            btn.Margin = new Padding(3, 3, 5, 4);
            btn.FlatStyle = FlatStyle.Flat;
            btn.Cursor = Cursors.Hand;
            ApplyComboButtonStyle(btn, isActive);
            return btn;
        }

        private static void ApplyComboButtonStyle(Button btn, bool isActive)
        {
            if (isActive)
            {
                btn.BackColor = ColorTealAccent;
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                btn.FlatAppearance.BorderColor = ColorTealAccent;
                btn.FlatAppearance.BorderSize = 1;
            }
            else
            {
                btn.BackColor = Color.White;
                btn.ForeColor = ColorDarkSlate;
                btn.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                btn.FlatAppearance.BorderColor = ColorCardBorder;
                btn.FlatAppearance.BorderSize = 1;
            }
        }

        private void RefreshComboHighlights()
        {
            for (int i = 0; i < _comboButtons.Count; i++)
            {
                Button btn = _comboButtons[i];
                ComboDefinition combo = btn.Tag as ComboDefinition;
                bool active = combo != null && string.Equals(combo.Id, _activeComboId, StringComparison.OrdinalIgnoreCase);
                ApplyComboButtonStyle(btn, active);
            }
        }

        private static Label CreateBadge(string text, Color color)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            lbl.ForeColor = Color.White;
            lbl.BackColor = color;
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.Size = new Size(text.Length * 7 + 12, 18);
            return lbl;
        }

        private static Button AddQuickToolButton(Panel parent, string text, ref int y, EventHandler onClick)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = new Point(14, y);
            btn.Size = new Size(228, 36);
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(10, 0, 0, 0);
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = ColorDarkButton;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = Color.FromArgb(90, 105, 118);
            btn.FlatAppearance.BorderSize = 1;
            btn.Cursor = Cursors.Hand;
            btn.Click += onClick;
            parent.Controls.Add(btn);
            y += 44;
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

            _suppressCheckChanged = true;
            try
            {
                HashSet<string> targetIds = new HashSet<string>(combo.ActionIds, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, CheckBox> kvp in _actionCheckBoxes)
                {
                    kvp.Value.Checked = targetIds.Contains(kvp.Key);
                }
                _activeComboId = combo.Id;
                RefreshComboHighlights();
            }
            finally
            {
                _suppressCheckChanged = false;
            }

            UpdateSelectionCount();
        }

        private void OnClearSelectionClick(object sender, EventArgs e)
        {
            _suppressCheckChanged = true;
            try
            {
                foreach (KeyValuePair<string, CheckBox> kvp in _actionCheckBoxes)
                {
                    kvp.Value.Checked = false;
                }
                _activeComboId = "";
                RefreshComboHighlights();
            }
            finally
            {
                _suppressCheckChanged = false;
            }

            UpdateSelectionCount();
        }

        private void OnActionCheckChanged(object sender, EventArgs e)
        {
            if (!_suppressCheckChanged && !string.IsNullOrEmpty(_activeComboId))
            {
                _activeComboId = "";
                RefreshComboHighlights();
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
            _activeProgressForm = new ExecutionProgressForm(_engine, selected, this, _promoteProgressToPrimary);
            _activeProgressForm.ExecutionFinished += OnProgressExecutionFinished;
            _activeProgressForm.FormClosed += OnProgressFormClosed;

            _activeProgressForm.Show(this);
            _activeProgressForm.StartExecution();
        }

        private void OnProgressExecutionFinished(object sender, EventArgs e)
        {
            if (!IsDisposed && !_btnExecute.IsDisposed)
            {
                _btnExecute.Enabled = true;
            }
        }

        private void OnProgressFormClosed(object sender, FormClosedEventArgs e)
        {
            if (!IsDisposed && !_btnExecute.IsDisposed)
            {
                _btnExecute.Enabled = true;
            }
            DetachActiveProgressForm();
        }

        private void DetachActiveProgressForm()
        {
            if (_activeProgressForm != null)
            {
                _activeProgressForm.ExecutionFinished -= OnProgressExecutionFinished;
                _activeProgressForm.FormClosed -= OnProgressFormClosed;
                _activeProgressForm = null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            DetachActiveProgressForm();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetachActiveProgressForm();
            }
            base.Dispose(disposing);
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
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate()
                        {
                            if (IsDisposed)
                            {
                                return;
                            }

                            if (!_btnBackupDrivers.IsDisposed)
                            {
                                _btnBackupDrivers.Enabled = true;
                                _btnBackupDrivers.Text = "Backup dos Drivers (pnputil)";
                            }

                            MessageBox.Show(
                                this,
                                result.Summary,
                                result.Success ? "Backup Concluido" : "Aviso no Backup de Drivers",
                                MessageBoxButtons.OK,
                                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                        }));
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
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
                logForm.BackColor = ColorWarmBg;
                logForm.Icon = AppIconProvider.GetAppIcon();

                TextBox txt = new TextBox();
                txt.Multiline = true;
                txt.ReadOnly = true;
                txt.ScrollBars = ScrollBars.Both;
                txt.WordWrap = false;
                txt.Font = new Font("Consolas", 9F, FontStyle.Regular);
                txt.BackColor = ColorDarkSlate;
                txt.ForeColor = Color.FromArgb(242, 242, 242);
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
