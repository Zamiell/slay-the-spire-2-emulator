using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;

namespace MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;

public class RewardSetSkippedMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public RunLocation location;

    public int setId;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public bool ShouldBuffer => true;

    public RunLocation Location => location;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(location);
        writer.WriteInt(setId);
    }

    public void Deserialize(PacketReader reader)
    {
        location = reader.Read<RunLocation>();
        setId = reader.ReadInt();
    }
}
