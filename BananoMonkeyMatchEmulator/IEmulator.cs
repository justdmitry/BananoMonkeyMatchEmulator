namespace BananoMonkeyMatchEmulator
{
    using System.Threading.Tasks;

    public interface IEmulator
    {
        int ExceptionCount { get; set; }

        Task RunAsync();
    }
}
