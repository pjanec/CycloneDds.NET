[BUG] The IDL codegen has a bug with non-sequential enums — value gap at 3  causes the enum entries after to use `@value()`
annotations which confuses the idlc union case generator.
The IDL generator is using the field's position in the struct (0, 1, 2, 3, 4) instead of the actual discriminant
 values (0, 1, 2, 4, 5). So when it encounters symbol A at index 3, it's grabbing the wrong discriminant value,
 and when it gets to symbol C at index 4, it's using the numeric value 5 as a fallback. The gap from removing
 symbol B is causing the indices and values to misalign.


[BUG] DdsMon In sample Detail the Sender tab does not show NodeId components ApplicationId and DomainId.
Also the ip address is empty.

Only the following is shown:

    ProcessId: 187572
    Machine: PETRJAN-226-LP
    IP:

The SenderIdentity messages sent are like follows:

    {
      "ParticipantGuid": {
        "High": -3859426189079408639,
        "Low": -4539346945406751504
      },
      "AppDomainId": 0,
      "AppInstanceId": 400,
      "ComputerName": "PETRJAN-226-LP",
      "ProcessName": "Hrot.ClusterRunner",
      "ProcessId": 187572
    }


[BUG] DdsMon when i click the Save icon on the All Samples window the 'Save File' dialog opens.
It defaults to my user folder. No matter what folder i click it chanes the folder for a split
of the second but then it returns to my user folder, making it the only folder where i can save
the output file.
Same happes if i click the drive letter button ("D:\") It shows the content of drive D for half
a second but then return to my user folder C:\Users\[my.name]...


[TODO] Upgrade to cyclone dds 11.0.







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



[BUG] The design and layout of the ECS panels is ugly and scamped, looking like from the last century.
SHould be modern, keeping same style as other panels, using icons, no dense layouts

[BUG] The ECS plugin should present just the main entity list window in the plugin menu, not each window separately.
Note the other windows are DEPENDENT on the entity window (must be opened with parameters - for what entity)






[IMPROVEMENT] custom quick-filter pull down
It whould be nice if in the samples panel there was a button with a pull down arrow opening some kind of predefined filters
that can be quickly applied; the filters must be additive, the pull down would show checkbox list of the filter names;
unchecking would remove that filter part; maybe filters applied this way would not be shown in the filter textbox as it would
be extremely difficult or impossible to parse this expression and remove just relevant parts. plugins should be able to add new filters
to this list; the filters should be also user editable and those created by the user should be saved in the workspace json file.
