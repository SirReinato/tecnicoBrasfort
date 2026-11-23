using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.UI
{
    public class ActionHelpDialog : Form
    {
        private readonly TextBox _txtCommand;
        private readonly Label _lblCopyFeedback;

        public ActionHelpDialog(ActionDefinition action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            Text = "Detalhes da Acao - " + action.Name;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 340);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            Label lblTitle = new Label();
            lblTitle.Text = action.Name;
            lblTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblTitle.Location = new Point(18, 16);
            lblTitle.Size = new Size(524, 24);
            Controls.Add(lblTitle);

            Label lblMeta = new Label();
            lblMeta.Text = BuildMetaText(action);
            lblMeta.ForeColor = Color.DimGray;
            lblMeta.Location = new Point(18, 44);
            lblMeta.Size = new Size(524, 20);
            Controls.Add(lblMeta);

            Label lblDescHeader = new Label();
            lblDescHeader.Text = "O que esta acao faz:";
            lblDescHeader.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblDescHeader.Location = new Point(18, 74);
            lblDescHeader.Size = new Size(524, 20);
            Controls.Add(lblDescHeader);

            Label lblDesc = new Label();
            lblDesc.Text = action.Description;
            lblDesc.Location = new Point(18, 96);
            lblDesc.Size = new Size(524, 48);
            Controls.Add(lblDesc);

            Label lblImpact = new Label();
            lblImpact.Text = BuildImpactText(action);
            lblImpact.ForeColor = action.DropsRemoteAccess || action.IsDestructive ? Color.Firebrick : Color.DarkSlateGray;
            lblImpact.Location = new Point(18, 150);
            lblImpact.Size = new Size(524, 24);
            Controls.Add(lblImpact);

            Label lblCmdHeader = new Label();
            lblCmdHeader.Text = action.IsNativeApiEquivalent
                ? "Comando equivalente para rodar no CMD / Terminal:"
                : "Comando executado no CMD:";
            lblCmdHeader.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblCmdHeader.Location = new Point(18, 182);
            lblCmdHeader.Size = new Size(524, 20);
            Controls.Add(lblCmdHeader);

            _txtCommand = new TextBox();
            _txtCommand.Text = action.CommandText;
            _txtCommand.ReadOnly = true;
            _txtCommand.Multiline = true;
            _txtCommand.ScrollBars = ScrollBars.Vertical;
            _txtCommand.Font = new Font("Consolas", 9.5F, FontStyle.Regular);
            _txtCommand.BackColor = Color.WhiteSmoke;
            _txtCommand.Location = new Point(18, 206);
            _txtCommand.Size = new Size(524, 68);
            Controls.Add(_txtCommand);

            _lblCopyFeedback = new Label();
            _lblCopyFeedback.Location = new Point(18, 290);
            _lblCopyFeedback.Size = new Size(280, 24);
            _lblCopyFeedback.ForeColor = Color.DarkGreen;
            _lblCopyFeedback.Text = "";
            Controls.Add(_lblCopyFeedback);

            Button btnCopy = new Button();
            btnCopy.Text = "Copiar comando";
            btnCopy.Location = new Point(312, 286);
            btnCopy.Size = new Size(132, 30);
            btnCopy.Click += OnCopyClick;
            Controls.Add(btnCopy);

            Button btnClose = new Button();
            btnClose.Text = "Fechar";
            btnClose.Location = new Point(452, 286);
            btnClose.Size = new Size(90, 30);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }

        private void OnCopyClick(object sender, EventArgs e)
        {
            if (TryCopyToClipboard(_txtCommand.Text))
            {
                _lblCopyFeedback.ForeColor = Color.DarkGreen;
                _lblCopyFeedback.Text = "Comando copiado para a area de transferencia.";
            }
            else
            {
                _lblCopyFeedback.ForeColor = Color.Firebrick;
                _lblCopyFeedback.Text = "Nao foi possivel acessar a area de transferencia.";
            }
        }

        public static bool TryCopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            try
            {
                Clipboard.SetDataObject(text, true, 5, 100);
                return true;
            }
            catch (ExternalException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string BuildMetaText(ActionDefinition action)
        {
            string duration = string.IsNullOrEmpty(action.EstimatedDuration) ? "Variavel" : action.EstimatedDuration;
            return string.Format("Categoria: {0}  |  Tempo estimado: {1}", action.Category, duration);
        }

        private static string BuildImpactText(ActionDefinition action)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Impacto: ");
            if (action.DropsRemoteAccess)
            {
                sb.Append("DERRUBA ACESSO REMOTO temporariamente. ");
            }
            if (action.RequiresReboot)
            {
                sb.Append("Requer reinicializacao ao concluir. ");
            }
            if (action.IsDestructive && !action.DropsRemoteAccess && !action.RequiresReboot)
            {
                sb.Append("Altera estado do sistema/servico. ");
            }
            if (!action.DropsRemoteAccess && !action.RequiresReboot && !action.IsDestructive)
            {
                sb.Append("Seguro / baixo impacto.");
            }
            return sb.ToString();
        }
    }
}
