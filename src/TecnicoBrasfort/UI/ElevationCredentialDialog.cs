using System;
using System.Drawing;
using System.Security;
using System.Windows.Forms;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.UI
{
    public class ElevationCredentialDialog : Form
    {
        private readonly string _exePath;
        private readonly TextBox _txtUser;
        private readonly TextBox _txtPassword;
        private readonly Label _lblStatus;
        private readonly Button _btnElevate;

        public bool TriggeredRunAsFallback { get; private set; }

        public ElevationCredentialDialog(string exePath)
        {
            _exePath = exePath ?? "";
            TriggeredRunAsFallback = false;

            Text = "Tecnico Brasfort - Elevacao Administrativa";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(450, 275);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            Icon = AppIconProvider.GetAppIcon();

            Label lblTitle = new Label();
            lblTitle.Text = "Privilegio de Administrador necessario";
            lblTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblTitle.Location = new Point(18, 14);
            lblTitle.Size = new Size(414, 24);
            Controls.Add(lblTitle);

            Label lblInfo = new Label();
            lblInfo.Text = "Esta estacao esta operando sem elevacao automatica. Informe sua credencial administrativa de TI (Workstation Administrators):";
            lblInfo.Location = new Point(18, 40);
            lblInfo.Size = new Size(414, 34);
            Controls.Add(lblInfo);

            Label lblUser = new Label();
            lblUser.Text = "Usuario (DOMINIO\\usuario, usuario@dominio ou .\\local):";
            lblUser.Location = new Point(18, 82);
            lblUser.Size = new Size(414, 20);
            Controls.Add(lblUser);

            _txtUser = new TextBox();
            _txtUser.Location = new Point(18, 104);
            _txtUser.Size = new Size(414, 26);
            if (!string.Equals(Environment.UserDomainName, Environment.MachineName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(Environment.UserDomainName))
            {
                _txtUser.Text = Environment.UserDomainName + "\\";
            }
            Controls.Add(_txtUser);

            Label lblPwd = new Label();
            lblPwd.Text = "Senha:";
            lblPwd.Location = new Point(18, 136);
            lblPwd.Size = new Size(414, 20);
            Controls.Add(lblPwd);

            _txtPassword = new TextBox();
            _txtPassword.Location = new Point(18, 158);
            _txtPassword.Size = new Size(414, 26);
            _txtPassword.UseSystemPasswordChar = true;
            Controls.Add(_txtPassword);

            _lblStatus = new Label();
            _lblStatus.Location = new Point(18, 188);
            _lblStatus.Size = new Size(414, 38);
            _lblStatus.ForeColor = Color.Firebrick;
            _lblStatus.Text = "";
            Controls.Add(_lblStatus);

            _btnElevate = new Button();
            _btnElevate.Text = "Elevar e Continuar";
            _btnElevate.Location = new Point(196, 232);
            _btnElevate.Size = new Size(144, 30);
            _btnElevate.Click += OnElevateClick;
            Controls.Add(_btnElevate);

            Button btnCancel = new Button();
            btnCancel.Text = "Sair";
            btnCancel.Location = new Point(348, 232);
            btnCancel.Size = new Size(84, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = _btnElevate;
            CancelButton = btnCancel;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _txtUser.Focus();
            _txtUser.SelectionStart = _txtUser.Text.Length;
            _txtUser.SelectionLength = 0;
        }

        private void OnElevateClick(object sender, EventArgs e)
        {
            ParsedCredentials creds = ElevationPolicy.ParseCredentials(
                _txtUser.Text,
                Environment.UserDomainName,
                Environment.MachineName);

            if (string.IsNullOrEmpty(creds.UserName))
            {
                _lblStatus.Text = "Informe o nome de usuario administrativo.";
                _txtUser.Focus();
                _txtUser.SelectionStart = _txtUser.Text.Length;
                _txtUser.SelectionLength = 0;
                return;
            }

            string rawPwd = _txtPassword.Text;
            if (string.IsNullOrEmpty(rawPwd))
            {
                _lblStatus.Text = "Informe a senha administrativa.";
                _txtPassword.Focus();
                return;
            }

            _btnElevate.Enabled = false;
            _lblStatus.Text = "Validando credenciais e elevando...";
            Application.DoEvents();

            try
            {
                using (SecureString secPwd = BuildSecureString(rawPwd))
                {
                    _txtPassword.Clear();
                    rawPwd = null;

                    bool isSplitTokenAdmin;
                    string errorMessage;
                    bool validated = ElevationService.ValidateAdminCredentials(
                        creds.Domain,
                        creds.UserName,
                        secPwd,
                        out isSplitTokenAdmin,
                        out errorMessage);

                    if (!validated && !isSplitTokenAdmin)
                    {
                        _lblStatus.Text = errorMessage;
                        _txtPassword.Focus();
                        return;
                    }

                    if (!isSplitTokenAdmin)
                    {
                        bool requiresRunAsFallback;
                        bool relaunched = ElevationService.RelaunchWithCredentials(
                            _exePath,
                            creds.Domain,
                            creds.UserName,
                            secPwd,
                            out requiresRunAsFallback,
                            out errorMessage);

                        if (relaunched)
                        {
                            DialogResult = DialogResult.OK;
                            Close();
                            return;
                        }

                        if (!requiresRunAsFallback)
                        {
                            _lblStatus.Text = errorMessage;
                            _txtPassword.Focus();
                            return;
                        }
                    }

                    bool cancelledByUser;
                    if (ElevationService.RelaunchWithRunAs(_exePath, out cancelledByUser, out errorMessage))
                    {
                        TriggeredRunAsFallback = true;
                        DialogResult = DialogResult.OK;
                        Close();
                        return;
                    }

                    _lblStatus.Text = cancelledByUser
                        ? "Solicitacao UAC cancelada pelo usuario."
                        : errorMessage;
                }
            }
            finally
            {
                _btnElevate.Enabled = true;
            }
        }

        private static SecureString BuildSecureString(string plainText)
        {
            SecureString sec = new SecureString();
            if (!string.IsNullOrEmpty(plainText))
            {
                for (int i = 0; i < plainText.Length; i++)
                {
                    sec.AppendChar(plainText[i]);
                }
            }
            sec.MakeReadOnly();
            return sec;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
