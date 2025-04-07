using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using IA.FrontEnd.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace IA.FrontEnd.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly CustomAuthStateProvider _authStateProvider;
        private readonly IJSRuntime _jsRuntime;
        private readonly string _apiBaseUrl;


        public AuthService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, IJSRuntime jsRuntime, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _authStateProvider = (CustomAuthStateProvider)authStateProvider;
            _jsRuntime = jsRuntime;
            _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7113";
        }

        public async Task<bool> IsAuthenticated()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            return authState?.User?.Identity?.IsAuthenticated ?? false;
        }

        public async Task Login()
        {
            await _jsRuntime.InvokeVoidAsync("open", $"{_apiBaseUrl}/api/auth/google-login", "_self");
        }

        public async Task HandleCallback(string token, string? email = null, string? name = null, string? internalId = null)
        {
            await _authStateProvider.SetTokenAsync(token);

            // Si tienes un método para guardar información adicional del usuario
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(name))
            {
                await SaveUserInfoLocally(email, name, internalId);
            }
        }

        private async Task SaveUserInfoLocally(string email, string name, string? internalId)
        {
            // Implementa la lógica para guardar información adicional
            // Puedes usar localStorage o algún servicio de estado
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userEmail", email);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userName", name);
            if (!string.IsNullOrEmpty(internalId))
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userInternalId", internalId);
            }
        }

        public async Task Logout()
        {
            await _authStateProvider.LogoutAsync();
        }

        public async Task<UserInfo?> GetUserInfo()
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return null;

                var userInfo = await _httpClient.GetFromJsonAsync<UserInfo>("api/auth/user-info");
                return userInfo;
            }
            catch
            {
                return null;
            }
        }
    }

    public class UserInfo
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
    }
}
