using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IA.WebAPI.Services
{
    /// <summary>
    /// Interfaz para el servicio de gestión de estado OAuth
    /// </summary>
    public interface IOAuthStateService
    {
        /// <summary>
        /// Crea un nuevo estado OAuth y lo almacena
        /// </summary>
        /// <returns>Estado generado</returns>
        string CreateState();

        /// <summary>
        /// Valida y consume un estado OAuth
        /// </summary>
        /// <param name="state">Estado a validar</param>
        /// <returns>True si el estado es válido, false en caso contrario</returns>
        bool ValidateState(string state);
    }

    /// <summary>
    /// Implementación del servicio de gestión de estado OAuth
    /// </summary>
    public class OAuthStateService : IOAuthStateService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<OAuthStateService> _logger;
        private const string CACHE_KEY_PREFIX = "OAuthState_";
        private readonly TimeSpan _stateExpiration = TimeSpan.FromMinutes(10);

        public OAuthStateService(IMemoryCache cache, ILogger<OAuthStateService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public string CreateState()
        {
            var state = Guid.NewGuid().ToString("N");
            var cacheKey = $"{CACHE_KEY_PREFIX}{state}";

            _cache.Set(cacheKey, DateTime.UtcNow, _stateExpiration);
            _logger.LogDebug("Estado OAuth creado: {State}, expira en {Expiration} minutos", state, _stateExpiration.TotalMinutes);

            return state;
        }

        /// <inheritdoc/>
        public bool ValidateState(string state)
        {
            if (string.IsNullOrEmpty(state))
            {
                _logger.LogWarning("Estado OAuth vacío o nulo");
                return false;
            }

            var cacheKey = $"{CACHE_KEY_PREFIX}{state}";

            if (_cache.TryGetValue(cacheKey, out DateTime timestamp))
            {
                _cache.Remove(cacheKey); // Consumir el estado para evitar reutilización
                _logger.LogInformation(
                    "Estado OAuth validado: {State}, creado hace {Age} segundos",
                    state,
                    (DateTime.UtcNow - timestamp).TotalSeconds);
                return true;
            }

            _logger.LogWarning("Estado OAuth inválido o expirado: {State}", state);
            return false;
        }
    }
}