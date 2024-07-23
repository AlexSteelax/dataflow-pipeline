using System.Threading.Channels;

namespace Steelax.DataflowPipeline.Common;

public delegate DataflowTask<TValue> ChannelConfigureHandler<TValue, TChannelValue>(Channel<TChannelValue> channel);