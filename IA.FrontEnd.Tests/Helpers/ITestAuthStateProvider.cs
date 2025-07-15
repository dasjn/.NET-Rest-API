namespace IA.FrontEnd.Tests.Helpers
{
    public interface ITestAuthStateProvider
    {
        Task<string> GetTokenAsync();
    }
}