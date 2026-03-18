[TODO] Upgrade to cyclone dds 11.0.

[BUG] DdsMonitor: too strict component type  def
in ddsmon-settings.json "ComponentTypeName" contains full assembly id including version like

    "ComponentTypeName": "DdsMonitor.Components.TopicExplorerPanel, DdsMonitor, Version=0.2.0.0, Culture=neutral, PublicKeyToken=null",

It should be just the full namespace & class, exclusding the version and culture etc. like:

    "ComponentTypeName": "DdsMonitor.Components.TopicExplorerPanel",

[IMPROVEMENT] DdsMonitor: sorted topic in topic sources
In the Topic Sources panel
    the table "Topics in selected assembly" 
      - should be alphabetically sorted by topic name
      - CLR type should show the namespace as the recond row (grayed a bit)


[IMPROVEMENT] DdsMonitor: full folder scan in topic sources
In the Topic Sources panel
  - the path could be a folder path (no concrete dll name)
     - system will scan all potential assemblies (*.dll and *.exe etc) automatically
     - the CLR type in "Topics in selected assembly" will show 3-lines (1. base type name, 2. namespace, 3. assembly file path)


[IMPROVEMENT] More useful schema compiler info message
schema compiler now writes "Running CycloneDDS Code Generator (Incremental)..." many times when compiling a larger project.
I need it to bring more information, like what part of the project is just being compiled