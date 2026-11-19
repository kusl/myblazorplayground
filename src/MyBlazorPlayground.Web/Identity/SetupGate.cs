using MyBlazorPlayground.Data.People;
using MyBlazorPlayground.Domain.Security;

namespace MyBlazorPlayground.Web.Identity;

internal sealed partial class SetupGate(PersonStore people, ILogger<SetupGate> logger)
{
    private readonly Lock _gate = new();
    private SetupCode? _code;
    private volatile bool _complete;

    public bool IsComplete => _complete;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var complete = await people.HasAdministratorAsync(cancellationToken);
        lock (_gate)
        {
            _complete = complete;
            _code = complete ? null : SetupCode.Create();
            if (_code is not null)
            {
                LogSetupCode(logger, _code.Value);
            }
        }
    }

    public bool Accepts(string? code)
    {
        lock (_gate)
        {
            return !_complete && _code is not null && _code.Matches(code);
        }
    }

    public void MarkComplete()
    {
        lock (_gate)
        {
            _complete = true;
            _code = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SETUP CODE: {Code} (open /setup on this site and enter it to create the first administrator)")]
    private static partial void LogSetupCode(ILogger logger, string code);
}
