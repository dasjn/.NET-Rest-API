using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
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
                return BuildAuthState(token);
            }
            catch
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _tokenKey);
                return _anonymous;
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
