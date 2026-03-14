[TODO] Upgrade to cyclone dds 11.0.

[TODO] typed enums
Add support for defining the bit size of the enum data type
In csharp DSL read the type specification

    public enum ESomething : byte
    
and convert it to @bit_bound annotation in IDL..

    @bit_bound(8)
    enum ESomething {
      ...
    };


When you use the @bit_bound(8) annotation with an enum in Cyclone DDS, the generated plain-C code typically maps the field to a uint8_t.

Mapping Behavior
 - In standard IDL-to-C mapping, an enum is often represented by a standard C enum type, which compilers usually default to 32 bits (int). However, Cyclone's IDL compiler (idlc) uses the @bit_bound annotation to optimize the storage and serialization size:
    - uint8_t: Used when @bit_bound is between 1 and 8 bits.
    - uint16_t: Used when @bit_bound is between 9 and 16 bits.
    - uint32_t: The default size if the annotation is omitted or set between 17 and 32 bits.


[TODO]
Add support for inline arrays specified using [InlineArray] attribute. Now we support unmanaged fixed size arrays.

[TODO] DdsMonitor should render just the currently selected union arm (using the discriminator field)
for union type fields and hide the other arm data parts that are generated as separate csharp fields but are not
currently selected via the discriminator field.
This should apply also to the dynamic building of editing fields for the dynamic panel used for sending samples.
Based on the currently selected value of the discriminator, only relevant csharp data struct field should be offered
editing and the other should be hidden from editing.

[TODO] Auto open of web-blowser, auto-close once browser disconnected.
the ddsmon can remove the https way and keep just the http one for simplicity. security is not an issue in ddsmon case.
by default (with no command line args) the ddsmon should open the default web browser 
 using the http localhost address and correct port.
And to terminate when the browser disconnects (user closes the browser tab)
or when the browser fails to connect within given timeout (browser failed to open).
Command line arguments to define the browser connection and disconnection timeouts.


[TODO] Default topic name based on full namespace
Topic name must by default (if [DdsTopic] attribute does NOT specify the name)
include full namespace name of the message struct in Csharp notation, using underscore instead of dots.
This allows using StartsWith filter to cover whole family of topcis.

[TODO] Multi-participant reception
The ddsmon should support listening to different partitions and domains at the same time, using multiple participants,
each assigned a concrete domain id and partition name.
The ddsmon should read the samples from all participants, and stamp them with a unique global sample ordinal
so that each sample is uniquely identifiable.

The sample information kept by the ddsmon should include the link to the participant (maybe just some index to global
participant table if not a full reference), allowing for retrieving the domain id and partition name for each sample.

These both need to be serialized to json for each sample (next to the sample ordinal and incoming time stamp.) 

The sample filter used for the sample replay must be able to address these fields (ordinal, partition, domain, incoming time stamp).

DdsMonitor sample detail panel should show this information in its 'Sample Info' tab.


[TODO] Start/Pause/Reset, Domains/partitions indicator, 
Dds monitor should show the "Start/Pause/Reset" buttons (colored icons, like a tape recorder) directly in ints main menu line,
after all main menu items.
There should also be an indicator what domains and partitions are currently actively listened to (based on the participants).
By clicking this indicator a dialog should open allowing to add/remove/edit the listening participants. 
This dialog must be accessible also from the 'Windows' main menu.
The participant settings should be valid just until the ddsmon terminates.
Ddsmon need new command line arguments to specify the participant parameters (domain, partition) for each participant.
By default the ddsmon should start listening on the default domain and empty partition.




[TODO] headless recorder/replay
ddsmon to run in headless mode
  1. Recording the traffic to a json file according to given filter expression.
  2. Replay the traffic from given json file according to given filter expression
   including the time range (or sample ordinal range)
   The filter must support string comparions "StartsWith" applicable to topic name

new CLI Options:
  1. replay rate (float; 1=real time, 2=2 times realtime etc)
  2. filter exression to be applied to the recorded or replayed data.
        - On live recording, Filter removes non-matching incoming samples (not allocating ordinal for them)
        - On replay, filter removes non-matching samples from the input file (not counting them into total frame count)




[BUG] dds mon
(json tab on sample detail) - shows enums in the json  as numbers - must be string
(tree tab on samkle detail panel) - inline array not expanded

