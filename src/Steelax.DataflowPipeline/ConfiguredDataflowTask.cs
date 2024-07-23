using System.Threading.Channels;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline;

public abstract class ConfiguredDataflowTask<TValue>
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="capacity"></param>
    /// <param name="bufferFullMode"></param>
    /// <returns></returns>
    public abstract DataflowTask<TValue> AsBounded(int capacity, BufferFullMode bufferFullMode = BufferFullMode.Wait);
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract DataflowTask<TValue> AsUnbounded();
}

public class ConfiguredDataflowTask<TValue, TChannelValue>(ChannelConfigureHandler<TValue, TChannelValue> configurator)
    : ConfiguredDataflowTask<TValue>
{
    private Channel<TChannelValue>? _channel;

    public Channel<TChannelValue> GetConfiguredChannel() => _channel ?? throw new NullReferenceException();

    public override DataflowTask<TValue> AsBounded(int capacity, BufferFullMode bufferFullMode = BufferFullMode.Wait)
    {
        _channel = CreateBoundedChannel(capacity, bufferFullMode);
        return configurator(_channel);
    }
    
    public override DataflowTask<TValue> AsUnbounded()
    {
        _channel = CreateUnboundedChannel();
        return configurator(_channel);
    }

    private static Channel<TChannelValue> CreateBoundedChannel(int capacity, BufferFullMode bufferFullMode) => 
        Channel.CreateBounded<TChannelValue>(new BoundedChannelOptions(capacity)
        {
            FullMode = bufferFullMode switch
            {
                BufferFullMode.Wait => BoundedChannelFullMode.Wait,
                BufferFullMode.DropNewest => BoundedChannelFullMode.DropNewest,
                BufferFullMode.DropOldest => BoundedChannelFullMode.DropOldest,
                BufferFullMode.DropWrite => BoundedChannelFullMode.DropWrite,
                _ => throw new NotImplementedException()
            },
            SingleReader = true,
            SingleWriter = true
        });
    
    private static Channel<TChannelValue> CreateUnboundedChannel() =>
        Channel.CreateUnbounded<TChannelValue>(new UnboundedChannelOptions()
        {
            SingleReader = true,
            SingleWriter = true
        });
}