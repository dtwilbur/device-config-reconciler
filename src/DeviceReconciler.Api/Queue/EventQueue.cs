using System.Threading.Channels;
using DeviceReconciler.Core.Models;

namespace DeviceReconciler.Api.Queue;

// Wraps Channel<T> so Program.cs doesn't need to fuss with channel options.
// In-process only, no broker - an event is gone if the process dies before
// the worker drains it.
public class EventQueue
{
    private readonly Channel<DesiredStateEvent> _channel =
        Channel.CreateUnbounded<DesiredStateEvent>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelWriter<DesiredStateEvent> Writer => _channel.Writer;
    public ChannelReader<DesiredStateEvent> Reader => _channel.Reader;
}
