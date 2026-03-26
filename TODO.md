[TODO] Upgrade to cyclone dds 11.0.





[IMPROVEMENT] dds mon enum field values should be shown in different color than numeric field values and different
than string values and different for struct headers to the field data type is recignizable at first glance
and not accidentally visually mistaken with string representation for example




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

[BUG] The design and layout of the ECS panels is ugly and clearly under-worked, looking like for last century.
SHould be modern, keeping same style as other panels, using icons, no dense layouts

