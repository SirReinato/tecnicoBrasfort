using System;
using System.Drawing;
using System.Windows.Forms;
using TecnicoBrasfort.Core;

namespace TecnicoBrasfort.UI
{
    public class AccessGateDialog : Form
    {
        private const int MaxAttempts = 3;

        private readonly AppConfig _config;
        private readonly TextBox _txtPassword;
        private readonly Label _lblStatus;
        private readonly Button _btnEnter;
        private int _attempts;

        public AccessGateDialog(AppConfig config)
        {
            _config = config;
            _attempts = 0;

            Text = "Tecnico Brasfort - Autenticacao";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 195);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            Icon = AppIconProvider.GetAppIcon();

            Label lblTitle = new Label();
            lblTitle.Text = "Acesso restrito a equipe de Suporte TI";
            lblTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblTitle.Location = new Point(18, 16);
            lblTitle.Size = new Size(384, 24);
            Controls.Add(lblTitle);

            Label lblPrompt = new Label();
            lblPrompt.Text = "Digite a senha de acesso ao painel:";
            lblPrompt.Location = new Point(18, 48);
            lblPrompt.Size = new Size(384, 20);
            Controls.Add(lblPrompt);

            _txtPassword = new TextBox();
            _txtPassword.Location = new Point(18, 74);
            _txtPassword.Size = new Size(384, 26);
            _txtPassword.UseSystemPasswordChar = true;
            Controls.Add(_txtPassword);

            _lblStatus = new Label();
            _lblStatus.Location = new Point(18, 106);
            _lblStatus.Size = new Size(384, 34);
            _lblStatus.ForeColor = Color.Firebrick;
            _lblStatus.Text = "";
            Controls.Add(_lblStatus);

            _btnEnter = new Button();
            _btnEnter.Text = "Entrar";
            _btnEnter.Location = new Point(226, 148);
            _btnEnter.Size = new Size(84, 30);
            _btnEnter.Click += OnEnterClick;
            Controls.Add(_btnEnter);

            Button btnCancel = new Button();
            btnCancel.Text = "Sair";
            btnCancel.Location = new Point(318, 148);
            btnCancel.Size = new Size(84, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = _btnEnter;
            CancelButton = btnCancel;
        }

        private void OnEnterClick(object sender, EventArgs e)
        {
            string pwd = _txtPassword.Text;
            if (AccessGate.VerifyPassword(pwd, _config))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _attempts++;
            _txtPassword.Clear();
            _txtPassword.Focus();

            int remaining = MaxAttempts - _attempts;
            if (remaining <= 0)
            {
                MessageBox.Show(
                    this,
                    "Numero maximo de tentativas excedido. O aplicativo sera encerrado.",
                    "Acesso Negado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            _lblStatus.Text = string.Format("Senha incorreta. Tentativas restantes: {0}.", remaining);
        }
    }
}
