namespace Steelax.DataflowPipeline.Common;

/// <summary>
/// Specifies the behavior to use when writing to a buffer that is already full.
/// </summary>
public enum BufferFullMode
{
    /// <summary>
    /// Waits for space to be available in order to complete the write operation.
    /// </summary>
    Wait = 0,
 
    /// <summary>
    /// Removes and ignores the newest item in the channel in order to make room for the item being written.
    /// </summary>
    DropNewest = 1,
   
    /// <summary>
    /// Removes and ignores the oldest item in the channel in order to make room for the item being written.
    /// </summary>
    DropOldest = 2,
  
    /// <summary>
    /// Drops the item being written.
    /// </summary>
    DropWrite = 3
}
