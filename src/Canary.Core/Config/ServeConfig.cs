namespace Canary.Core.Config;

public sealed class ServeConfig
{
    // Default port `canary serve` binds to when --port isn't passed on the
    // command line (which still wins over this when given). Deliberately
    // not 8080/3000/5000/8000/etc -- those are exactly the ports every
    // other local dev server on a machine is already fighting over.
    public int Port { get; set; } = 6913;
}
