using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IA.WebAPI.Models.Auth
{
    /// <summary>
    /// Modelo para representar información del usuario autenticado
    /// </summary>
    public class UserInfoModel
    {
        /// <summary>
        /// ID único del usuario
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Correo electrónico del usuario
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Nombre del usuario
        /// </summary>
        public string? Name { get; set; }
    }

    /// <summary>
    /// Respuesta de autenticación con token
    /// </summary>
    public class AuthenticationResponse
    {
        /// <summary>
        /// Token JWT para autenticación
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Información del usuario
        /// </summary>
        public UserInfoModel User { get; set; } = new UserInfoModel();
    }

    /// <summary>
    /// Respuesta de tokens de Google
    /// </summary>
    public class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta de información de usuario de Google
    /// </summary>
    public class GoogleUserInfoResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("verified_email")]
        public bool VerifiedEmail { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("given_name")]
        public string GivenName { get; set; } = string.Empty;

        [JsonPropertyName("family_name")]
        public string FamilyName { get; set; } = string.Empty;

        [JsonPropertyName("picture")]
        public string Picture { get; set; } = string.Empty;

        [JsonPropertyName("locale")]
        public string Locale { get; set; } = string.Empty;
    }
}