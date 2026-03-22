[TODO] Upgrade to cyclone dds 11.0.

[BUG] On opening/restoring from minimized the Topics window, all topics get subscribed even if they were not before.
Opening a topics window (or any other window) should NOT change the topic subscription! Subscription is the
backend state and opening a window should not change anything on it!


[BUG] On Sample detail tab, 'Sender identity' tab always show 'No sender data' although the sender tracking is
turned on for sending participants.


[IMPROVEMENTS] The tree tab of sample detail panel should have icons for "collapse all" and "expand all".


[IMPROVEMENT] dds mon enum field values should be shown in different color than numeric field values and different
than string values and different for struct headers to the field data type is recignizable at first glance
and not accidentally visually mistaken with string representation for example


[IMPROVEMENT]
Topic Properties needs to show
 - real DDS topic name string (not just CLR type)
 - QoS settings

[IMPROVEMENT]
The controls (minimize, close) in the title bar of the panels are difficult to see. Better if they could have
some transparent colored background.

[BUG]
The sample detail that was linked to the all samples panel, on startup (after restoring size/position etc)
shows up but is usually NOT linked to the all samples panel. To restore the link, it needs to be closed
and reopened by double-clicking a sample in the all samples panel.

[IMPROVEMENT]
On the samples panel the filter expression dialog is very small to serve for convenient editing.
There should be a button that shows the expression is much large edit box (maybe just by changing
 the layout of the dialog so that the edit field is one next line and spans full width of the panel
 and has multipe lines.


[IMPROVEMENT]
The self-send topics should be extended to cover all possible data types (for testing purposes) including
 - unions of various data types
 - list of unions
 - Bit bound enums
 - Guid
 - Datetime
 - Arrays
 - Lists
 - Fixed size arrays
 - Bounded arrays
 - Fixed size string
 - etc.

At least one field should vary each sample.

[IMPROVEMENT]
Plugin system need deep revamping for better decoupling and flexibility,
see https://notebooklm.google.com/notebook/f41a7c82-1eaf-4608-b079-98f1d9e1d7ea

[BUG] The design and layout of the BDC panels is ugly and clearly under-worked, looking like for last century.
SHould be modern, keeping same style as other panels, using icons, no dense layouts

