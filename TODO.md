[TODO] Upgrade to cyclone dds 11.0.


[IMPROVEMENTS] The tree tab of sample detail panel should have icons for "collapse all" and "expand all"


[IMPROVEMENT] dds mon enum field values should be shown in different color than numeric field values and different
than string values and different for struct headers to the field data type is recignizable at first glance
and not accidentally visually mistaken with string representation for example


[IMPROVEMENTS] measuring the bandwidth of incoming traffic
I need the dds monitor to show how big the volume of the incoming traffic is.
It does not need to be byte precise, just estimate is enough.
It should be shown in the main menu line next to the domain/partition indicator.
Something like "345KB/s" or "2.65MB/s" (3 digits, human readable autoselection of units)

1. Leverage the existing GetNativeSize code generation (Best Estimate)
The CycloneDDS.CodeGen tool already analyzes your types and emits a highly optimized public static int GetNativeSize(in T source) method for every [DdsTopic] and [DdsStruct]
. This method calculates the base native C-struct size and recursively adds the sizes of dynamic collections, strings, and subfields
.
The SampleData record already has a SizeBytes property
, but in DynamicReader<T>.cs, it is currently hardcoded to 0 (UnknownSizeBytes)
. You can easily wire the generated sizer into the reader:
Update DynamicReader<T>.cs: Extract the delegate via reflection (just as DdsWriter<T> does
) and apply it to incoming payloads:
private delegate int GetNativeSizeDelegate(in T sample);
private static readonly GetNativeSizeDelegate? _nativeSizer;

static DynamicReader()
{
    var method = typeof(T).GetMethod("GetNativeSize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    if (method != null) 
    {
        _nativeSizer = (GetNativeSizeDelegate)Delegate.CreateDelegate(typeof(GetNativeSizeDelegate), method);
    }
}
Then, in EmitSample(DdsSample<T> sample), calculate the size before creating the SampleData:
var payload = sample.Data;
int estimatedSize = _nativeSizer != null && payload != null ? _nativeSizer(payload) : UnknownSizeBytes;

var tempSample = new SampleData
{
    Ordinal = 0,
    Payload = payload!,
    TopicMetadata = TopicMetadata,
    SampleInfo = sample.Info,
    Timestamp = DateTime.UtcNow,
    SizeBytes = estimatedSize, // Populated here!
    DomainId = _config?.DomainId ?? 0,
    // ...
};
2. UI Integration
Because SampleData.SizeBytes is populated, the SamplesPanel will automatically support it. TopicMetadata.cs already defines a synthetic field named "Size [B]" which binds directly to SampleData.SizeBytes
. You will be able to select "Size [B]" in the Columns Picker and sort by it.
To show incoming bandwidth (e.g., KB/s): You can use the existing RingBuffer class
, which is currently used to track message frequency for the topic sparklines
.
Add a second RingBuffer dictionary tracking bytes: private readonly Dictionary<Type, RingBuffer> _bandwidthSparks.
When flushing the timer, sum the SizeBytes of new samples and push the byte count into the buffer.
Display sum / 1024.0 in a new UI column or widget as "KB/s".






