using BelgaAuthAPI.Data;
using BelgaAuthAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BelgaAuthAPI.Scripts
{
    // Script para criar licenças manualmente
    // Execute este código em um controller ou console app para gerar licenças
    
    public class LicenseGenerator
    {
        public static async Task<string> CreateLicense(
            AuthDbContext db, 
            string subscriptionName = "Premium", 
            int daysValid = 30,
            int? resellerId = null,
            string? customKey = null,
            string? resellerPrefix = null)
        {
            string licenseKey;
            
            if (!string.IsNullOrEmpty(customKey))
            {
                // Usa a chave personalizada fornecida
                licenseKey = customKey;
            }
            else
            {
                // Gera uma chave aleatória, com prefixo se fornecido
                licenseKey = GenerateLicenseKey(resellerPrefix);
            }
            
            // Verifica se a chave já existe
            if (await db.Licenses.AnyAsync(l => l.Key == licenseKey))
            {
                throw new InvalidOperationException($"A chave '{licenseKey}' já existe no banco de dados.");
            }
            
            var license = new License
            {
                Key = licenseKey,
                SubscriptionName = subscriptionName,
                DaysValid = daysValid,
                IsUsed = false,
                CreatedDate = DateTime.UtcNow,
                ResellerId = resellerId
            };

            db.Licenses.Add(license);
            await db.SaveChangesAsync();

            return licenseKey;
        }

        private static string GenerateLicenseKey(string? prefix = null)
        {
            var random = new Random();
            
            if (!string.IsNullOrEmpty(prefix))
            {
                // Se tem prefixo, gera: PREFIXO-XXXX-XXXX-XXXX
                var parts = new List<string> { prefix.ToUpper() };
                
                for (int i = 0; i < 3; i++)
                {
                    var part = "";
                    for (int j = 0; j < 4; j++)
                    {
                        part += (char)('A' + random.Next(26));
                    }
                    parts.Add(part);
                }
                
                return string.Join("-", parts);
            }
            else
            {
                // Gera uma chave no formato: XXXX-XXXX-XXXX-XXXX
                var parts = new List<string>();
                
                for (int i = 0; i < 4; i++)
                {
                    var part = "";
                    for (int j = 0; j < 4; j++)
                    {
                        part += (char)('A' + random.Next(26));
                    }
                    parts.Add(part);
                }
                
                return string.Join("-", parts);
            }
        }
    }
}

