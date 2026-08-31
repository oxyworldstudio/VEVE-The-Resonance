using Unity.Netcode;

namespace VEVE.Net
{
    /// <summary>
    /// Wire contract for the journal command - all fixed-size primitives, one
    /// generic SerializeValue pass; zero strings/allocs on the transport path.
    /// </summary>
    public partial struct NetCommand : INetworkSerializable
    {
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            byte t = (byte)type;
            serializer.SerializeValue(ref t);
            type = (NetCommandType)t;

            serializer.SerializeValue(ref senderId);
            serializer.SerializeValue(ref frame);
            serializer.SerializeValue(ref seq);
            serializer.SerializeValue(ref i0);
            serializer.SerializeValue(ref i1);
            serializer.SerializeValue(ref f0);
            serializer.SerializeValue(ref f1);

            float x = world.x, y = world.y, z = world.z;
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref z);
            world = new VectorPack { x = x, y = y, z = z };
        }
    }
}
