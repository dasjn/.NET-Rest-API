using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace IA.FrontEnd.Auth
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly HttpClient _httpClient;
        private readonly string _tokenKey = "authToken";
        private AuthenticationState _anonymous => new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        public CustomAuthStateProvider(IJSRuntime jsRuntime, HttpClient httpClient)
        {
            _jsRuntime = jsRuntime;
            _httpClient = httpClient;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", _tokenKey);

            if (string.IsNullOrEmpty(token))
                return _anonymous;

            try
            {
                // Check if token is close to expiration
                if (IsTokenExpired(token))
                {
                    // Attempt to refresh token
                    var newToken = await RefreshTokenAsync(token);
                    if (!string.IsNullOrEmpty(newToken))
                    {
                        token = newToken;
                        await SetTokenAsync(token);
                    }
                    else
                    {
                        // If token refresh fails, log out
                        await LogoutAsync();
                        return _anonymous;
                    }
                }

                return BuildAuthState(token);
            }
            catch
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _tokenKey);
                return _anonymous;
            }
        }

        private bool IsTokenExpired(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var expirationTime = jwtToken.ValidTo;

            // Check if token will expire in the next 5 minutes
            return expirationTime <= DateTime.UtcNow.AddMinutes(5);
        }

        private async Task<string?> RefreshTokenAsync(string expiredToken)
        {
            try
            {
                // Create an object to send as JSON
                var refreshRequest = new { token = expiredToken };

                // Use PostAsJsonAsync to correctly serialize and send the request
                var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh-token", refreshRequest);

                if (response.IsSuccessStatusCode)
                {
                    var newToken = await response.Content.ReadAsStringAsync();
                    return newToken;
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token refresh error: {ex.Message}");
                return null;
            }
        }

        public async Task SetTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _tokenKey);
                NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
                return;
            }

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _tokenKey, token);
            var authState = BuildAuthState(token);
            NotifyAuthenticationStateChanged(Task.FromResult(authState));
        }

        public async Task<string> GetTokenAsync()
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", _tokenKey);
        }

        public async Task LogoutAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _tokenKey);
            _httpClient.DefaultRequestHeaders.Authorization = null;
            NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
        }

        private AuthenticationState BuildAuthState(string token)
        {
            try
            {
                // Configurar el token en el HttpClient para todas las solicitudes
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Decodificar el token JWT
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var claims = jwtToken.Claims.ToList();

                // Añadir un claim personalizado para el ID interno
                var internalIdClaim = claims.FirstOrDefault(c => c.Type == "InternalId");
                if (internalIdClaim != null)
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, internalIdClaim.Value));
                }

                // Añadir la imagen de perfil como claim para fácil acceso
                var profilePictureClaim = claims.FirstOrDefault(c => c.Type == "ProfilePictureUrl");
                if (profilePictureClaim != null)
                {
                    // El claim ya existe, así que no es necesario añadirlo de nuevo
                }
                else
                {
                    // Intentar obtener la imagen del localStorage como fallback
                    var profilePictureUrl = _jsRuntime.InvokeAsync<string>("localStorage.getItem", "userProfilePictureUrl").Result;
                    if (!string.IsNullOrEmpty(profilePictureUrl))
                    {
                        claims.Add(new Claim("ProfilePictureUrl", profilePictureUrl));
                    }
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);

                return new AuthenticationState(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar token: {ex.Message}");
                throw;
            }
        }
    }
}