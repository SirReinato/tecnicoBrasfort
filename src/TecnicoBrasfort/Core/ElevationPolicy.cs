using System;

namespace TecnicoBrasfort.Core
{
    public enum ElevationDecision
    {
        AlreadyElevated,
        RelaunchRunAs,
        PromptCredentials,
        BlockedConsentDeny,
        BlockedLoop
    }

    public class UacPolicyState
    {
        public bool EnableLua { get; set; }
        public int ConsentPromptBehaviorUser { get; set; }

        public UacPolicyState()
        {
            EnableLua = true;
            ConsentPromptBehaviorUser = 3;
        }
    }

    public class ParsedCredentials
    {
        public string Domain { get; set; }
        public string UserName { get; set; }

        public ParsedCredentials()
        {
            Domain = "";
            UserName = "";
        }
    }

    public static class ElevationPolicy
    {
        public const string RunAsRetryFlag = "--elevated-runas";
        public const string CredRetryFlag = "--elevated-cred";

        public static ElevationDecision Evaluate(
            bool isElevated,
            UacPolicyState uac,
            bool hasRunAsRetryFlag,
            bool hasCredRetryFlag)
        {
            if (isElevated)
            {
                return ElevationDecision.AlreadyElevated;
            }

            if (hasCredRetryFlag)
            {
                return ElevationDecision.BlockedLoop;
            }

            if (hasRunAsRetryFlag)
            {
                return ElevationDecision.PromptCredentials;
            }

            UacPolicyState effectiveUac = uac ?? new UacPolicyState();
            if (effectiveUac.EnableLua && effectiveUac.ConsentPromptBehaviorUser == 0)
            {
                return ElevationDecision.BlockedConsentDeny;
            }

            return effectiveUac.EnableLua
                ? ElevationDecision.RelaunchRunAs
                : ElevationDecision.PromptCredentials;
        }

        public static ParsedCredentials ParseCredentials(
            string rawUser,
            string defaultDomain,
            string localMachineName)
        {
            ParsedCredentials result = new ParsedCredentials();
            if (string.IsNullOrEmpty(rawUser))
            {
                return result;
            }

            string trimmed = rawUser.Trim();
            if (trimmed.Length == 0)
            {
                return result;
            }

            int slashIdx = trimmed.IndexOf('\\');
            if (slashIdx >= 0)
            {
                string domPart = trimmed.Substring(0, slashIdx).Trim();
                string userPart = trimmed.Substring(slashIdx + 1).Trim();
                if (userPart.Length == 0)
                {
                    return result;
                }

                if (domPart == ".")
                {
                    result.Domain = string.IsNullOrEmpty(localMachineName)
                        ? Environment.MachineName
                        : localMachineName;
                }
                else
                {
                    result.Domain = domPart;
                }
                result.UserName = userPart;
                return result;
            }

            int atIdx = trimmed.IndexOf('@');
            if (atIdx >= 0)
            {
                string userBeforeAt = trimmed.Substring(0, atIdx).Trim();
                string domainAfterAt = trimmed.Substring(atIdx + 1).Trim();
                if (userBeforeAt.Length == 0 || domainAfterAt.Length == 0)
                {
                    return result;
                }

                // UPN exige lpszDomain = null no LogonUserW
                result.Domain = null;
                result.UserName = trimmed;
                return result;
            }

            result.Domain = string.IsNullOrEmpty(defaultDomain) ? "" : defaultDomain.Trim();
            result.UserName = trimmed;
            return result;
        }

        public static bool HasRetryFlag(string[] args, string flag)
        {
            if (args == null || string.IsNullOrEmpty(flag))
            {
                return false;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string BuildRetryArguments(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return "";
            }

            return ArgumentEscaper.JoinArguments(new string[] { flag });
        }

        public static string ExtractDriveLetterPrefix(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 2)
            {
                return "";
            }

            char drive = path[0];
            bool isLetter = (drive >= 'A' && drive <= 'Z') || (drive >= 'a' && drive <= 'z');
            if (!isLetter || path[1] != ':')
            {
                return "";
            }

            return path.Substring(0, 2);
        }

        public static string FormatElevationWin32Error(int nativeErrorCode, string exePath)
        {
            switch (nativeErrorCode)
            {
                case 1326:
                    return "Usuario ou senha incorretos.";
                case 1330:
                    return "A senha da conta informada expirou.";
                case 1331:
                    return "A conta informada esta desativada no sistema/AD.";
                case 1793:
                    return "A conta informada esta expirada.";
                case 1907:
                    return "A conta exige troca de senha no proximo logon antes de autenticar.";
                case 1909:
                    return "A conta informada esta bloqueada (Account Locked Out).";
                case 1311:
                    return "Nenhum controlador de dominio acessivel no momento. Conecte a rede/VPN ou use uma conta local (.\\usuario).";
                case 1385:
                    return "A conta informada nao possui permissao de logon interativo nesta estacao.";
                case 1058:
                    return "O servico Secondary Logon (seclogon) esta desativado nesta estacao. Habilite o servico ou abra o painel em uma sessao administrativa direta.";
                case 740:
                    return "Esta estacao exige elevacao UAC nativa para o processo informado.";
                case 5:
                case 267:
                case 362:
                    return string.Format(
                        "A conta de TI nao conseguiu acessar '{0}' (erro Win32 {1}). Copie a pasta para C:\\Temp ou C:\\Users\\Public\\Desktop, ou execute a partir de um caminho UNC da rede.",
                        string.IsNullOrEmpty(exePath) ? "o executavel" : exePath,
                        nativeErrorCode);
                case 1223:
                    return "Solicitacao de elevacao cancelada pelo usuario.";
                default:
                    return string.Format("Falha ao elevar o processo (erro Win32 {0}).", nativeErrorCode);
            }
        }
    }
}
