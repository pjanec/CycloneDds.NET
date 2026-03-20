[TODO] Upgrade to cyclone dds 11.0.


[IMPROVEMENTS] The tree tab of sample detail panel should have icons for "collapse all" and "expand all"


[IMPROVEMENT] dds mon enum field values should be shown in different color than numeric field values and different
than string values and different for struct headers to the field data type is recignizable at first glance
and not accidentally visually mistaken with string representation for example


[IMPROVEMENT]
Topic Properties needs to show
 - real DDS topic name string (not just CLR type)
 - QoS settings

[BUG] Sample memory never released
if in ddsmon i click "Reset" icon in the main menu bar  after receiving high amount of samples (taking high amount of memory),
 the samples disappear from the UI but the memory stays high.

If the memory stays at 10GB long after the reset, it means the Garbage Collector cannot release the memory because something in the application is still secretly holding hard references to those SampleData objects.
Based on the source code, there is a distinct memory leak occurring between the InstanceStore and the InstancesPanel. Here is exactly why the memory is not being released:
The Missing Event in InstanceStore: When you click the Reset icon, DdsBridge.ResetAll() is invoked, which calls _sampleStore?.Clear() and _instanceStore?.Clear()
. If you look at InstanceStore.Clear(), it simply empties its internal _topics dictionary, but it fails to publish a transition event to its _observable
.

The UI Remains Ignorant: Because InstanceStore.Clear() does not broadcast an event, the InstanceObserver attached to the InstancesPanel is never triggered
. As a result, the panel's internal _changeVersion counter never increments.
The Cache is Never Cleared: The InstancesPanel relies on a UI timer calling RefreshCounts() to update its grid
. However, RefreshCounts() only marks the view as "dirty" if _changeVersion has changed or if the total count explicitly changed
. Since the UI was never notified of the clear, _viewDirty remains false.
10GB of Held References: Because the view isn't marked dirty, InstancesPanel.EnsureView() exits early and skips calling _viewCache.Clear()
. The _viewCache is a List<InstanceRow>, and each InstanceRow contains a hard reference to the original SampleData and InstanceData objects
.

Conclusion: Even though the SampleStore successfully drops its references, the InstancesPanel's cache is unknowingly keeping a hard reference to every single sample. From the .NET Garbage Collector's perspective, those objects are "still in use" by the UI, so it refuses to release the 10GB of memory back to the OS.
To fix this, the source code for InstanceStore.Clear() would need to be updated to broadcast a specific "cleared" event so that the InstancesPanel knows to dump its _viewCache.

