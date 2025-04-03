namespace IA.WebAPI.Options
{
    public class AuthOptions
    {
        public const string SectionName = "Authentication";

        public string FrontendBaseUrl { get; set; } = string.Empty;
        public string LoginCallbackPath { get; set; } = string.Empty;
        public string LoginFailedPath { get; set; } = string.Empty;
        public JwtOptions Jwt { get; set; } = new JwtOptions();
        public GoogleAuthOptions Google { get; set; } = new GoogleAuthOptions();
    }
}