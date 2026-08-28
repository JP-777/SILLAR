using Sillar.Modules.Crm.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Modules.Crm.Documentation;

/// <summary>
/// Cuerpos de ejemplo de CRM para la documentación OpenAPI.
/// </summary>
public sealed class CrmExamples : ISchemaExamples
{
    /// <inheritdoc />
    public IReadOnlyDictionary<Type, string> Examples => Cuerpos;

    private static readonly Dictionary<Type, string> Cuerpos = new()
    {
        [typeof(CustomerLoginRequest)] = """
            {
              "email": "cliente@ejemplo.test",
              "password": "cambia-esto-por-una-tuya-de-varias-palabras"
            }
            """,

        [typeof(CustomerRegisterRequest)] = """
            {
              "fullName": "Cliente De Ejemplo",
              "email": "cliente@ejemplo.test",
              "password": "cambia-esto-por-una-tuya-de-varias-palabras",
              "phone": "+51 900 000 000"
            }
            """,

        [typeof(CustomerTokenRequest)] = """
            {
              "token": "token-recibido-en-el-enlace"
            }
            """,

        [typeof(CustomerPasswordResetRequest)] = """
            {
              "email": "cliente@ejemplo.test"
            }
            """,

        [typeof(CustomerPasswordResetConfirmRequest)] = """
            {
              "token": "token-recibido-en-el-enlace",
              "newPassword": "cambia-esto-por-una-tuya-de-varias-palabras"
            }
            """,

        [typeof(CustomerInvitationAcceptRequest)] = """
            {
              "token": "token-recibido-en-el-enlace",
              "password": "cambia-esto-por-una-tuya-de-varias-palabras"
            }
            """,

        [typeof(UpdateCustomerProfileRequest)] = """
            {
              "fullName": "Cliente De Ejemplo",
              "email": "cliente@ejemplo.test",
              "phone": "+51 900 000 000",
              "documentType": "dni",
              "documentNumber": "12345678"
            }
            """,

        [typeof(SaveCustomerAddressRequest)] = """
            {
              "label": "Casa",
              "addressLine": "Av. Ejército 100",
              "district": "Yanahuara",
              "province": "Arequipa",
              "department": "Arequipa",
              "reference": "Frente al parque",
              "isPreferred": true
            }
            """,
    };
}
