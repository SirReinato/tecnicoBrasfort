using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace TecnicoBrasfort.Core
{
    public static class AccessGate
    {
        public const int MinimumIterations = 100000;
        public const int SaltSizeBytes = 16;
        public const int HashSizeBytes = 32;

        public static bool IsConfigurationValid(ConfigLoadResult loadResult, out string error)
        {
            if (loadResult == null || loadResult.Config == null)
            {
                error = "Configuracao ausente (modo fail-closed).";
                return false;
            }

            if (!loadResult.FileFound)
            {
                error = "Arquivo config.json nao encontrado ao lado do executavel (modo fail-closed).";
                return false;
            }

            if (!loadResult.ParseSucceeded)
            {
                error = string.IsNullOrEmpty(loadResult.ErrorMessage)
                    ? "Arquivo config.json invalido (modo fail-closed)."
                    : loadResult.ErrorMessage;
                return false;
            }

            if (!loadResult.Config.RequireAccessPassword)
            {
                error = "";
                return true;
            }

            if (loadResult.Config.AccessIterations < MinimumIterations)
            {
                error = string.Format("AccessIterations deve ser >= {0} no config.json.", MinimumIterations);
                return false;
            }

            if (string.IsNullOrEmpty(loadResult.Config.AccessSaltBase64) ||
                string.IsNullOrEmpty(loadResult.Config.AccessHashBase64))
            {
                error = "AccessSaltBase64 e AccessHashBase64 sao obrigatorios quando RequireAccessPassword esta ativo.";
                return false;
            }

            try
            {
                byte[] saltBytes = Convert.FromBase64String(loadResult.Config.AccessSaltBase64);
                byte[] hashBytes = Convert.FromBase64String(loadResult.Config.AccessHashBase64);

                if (saltBytes.Length < SaltSizeBytes)
                {
                    error = string.Format("AccessSaltBase64 deve ter ao menos {0} bytes.", SaltSizeBytes);
                    return false;
                }

                if (hashBytes.Length != HashSizeBytes)
                {
                    error = string.Format("AccessHashBase64 deve ter exatamente {0} bytes.", HashSizeBytes);
                    return false;
                }

                error = "";
                return true;
            }
            catch (FormatException)
            {
                error = "AccessSaltBase64 ou AccessHashBase64 nao esta em formato Base64 valido.";
                return false;
            }
        }

        public static bool ShouldPromptForPassword(ConfigLoadResult loadResult, bool isElevatedAdmin)
        {
            string error;
            if (!IsConfigurationValid(loadResult, out error))
            {
                return true;
            }

            if (!loadResult.Config.RequireAccessPassword)
            {
                return false;
            }

            if (loadResult.Config.SkipAccessGateWhenElevated && isElevatedAdmin)
            {
                return false;
            }

            return true;
        }

        public static bool VerifyPassword(string password, AppConfig config)
        {
            if (config == null)
            {
                return false;
            }

            if (!config.RequireAccessPassword)
            {
                return true;
            }

            if (string.IsNullOrEmpty(password) ||
                config.AccessIterations < MinimumIterations ||
                string.IsNullOrEmpty(config.AccessSaltBase64) ||
                string.IsNullOrEmpty(config.AccessHashBase64))
            {
                return false;
            }

            try
            {
                byte[] saltBytes = Convert.FromBase64String(config.AccessSaltBase64);
                byte[] expectedHash = Convert.FromBase64String(config.AccessHashBase64);

                if (saltBytes.Length < SaltSizeBytes || expectedHash.Length != HashSizeBytes)
                {
                    return false;
                }

                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(
                    passwordBytes,
                    saltBytes,
                    config.AccessIterations,
                    HashAlgorithmName.SHA256))
                {
                    byte[] derived = pbkdf2.GetBytes(HashSizeBytes);
                    return FixedTimeEquals(derived, expectedHash);
                }
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static void GenerateHash(string password, int iterations, out string saltBase64, out string hashBase64)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("A senha nao pode ser vazia.", "password");
            }

            if (iterations < MinimumIterations)
            {
                throw new ArgumentOutOfRangeException(
                    "iterations",
                    string.Format("O numero de iteracoes deve ser >= {0}.", MinimumIterations));
            }

            byte[] saltBytes = new byte[SaltSizeBytes];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(saltBytes);
            }

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(
                passwordBytes,
                saltBytes,
                iterations,
                HashAlgorithmName.SHA256))
            {
                byte[] hashBytes = pbkdf2.GetBytes(HashSizeBytes);
                saltBase64 = Convert.ToBase64String(saltBytes);
                hashBase64 = Convert.ToBase64String(hashBytes);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }
    }
}
