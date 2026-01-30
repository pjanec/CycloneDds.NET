This is the list of tests failing the roundtrip tests (which is the ultimate test of the cyclone dds wrapper compatibility with proven
 native implementation):

 - <TODO: add here the list of failing tests>

Pls use the method described in tests\CsharpToC.Symmetry.Tests\FAST-ITERATION-GUIDE.md 
to fix the failing topics.

Use the following as a reference for finding out detail about the CDR stream format to know how to properly serializa and deserialize:
 - docs\cdr-serialization-rules.md
 - docs\cdr-byte-stream-analysis.md 

Once you succeed fixing the symmetry tests for some of the failing topics, always verify if it really works using the round trip tests,
by running the script
  - build_and_run_tests.ps1 -Filter <name_of_the_failing_test_case_in_xUnit_filter_format>
  
DO NOT RUN `build_and_run_tests.ps1` WITHOUT the `-Filter` parameter!!! That takes too long and produces tons of lines to stdout.
ALWAYS use `-Filter` parameter to limit the scope to the minimal set of test you need!

Continue fixing other failing topics until all failing ones are fixed.


CRITICAL:
Test Success after Serializer hot patching is not enough!

You have to fix the emitters, re-generate serializer/deserializer and confirm the succes using the round trip tests.

This is you success condition.

Do not stop until the fixed topics are passing the roundtrip tests after re-generating the serializaer/deserializer code from updated codegen.

