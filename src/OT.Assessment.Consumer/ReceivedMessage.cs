namespace OT.Assessment.Consumer;

/// Like a numbered ticket stapled to a parcel. When the parcel is safely stored, you hand back ticket number 42 to say parcel 42 is done.
public sealed record ReceivedMessage(ulong DeliveryTag, byte[] Body);
