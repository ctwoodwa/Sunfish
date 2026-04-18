namespace Marilo.PmDemo.Hubs;

public interface IPmDemoHubClient
{
    Task TaskUpdated(object payload);
}
