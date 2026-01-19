# BATCH-09.1: Golden Rig Union Verification (Complete)

**Batch Number:** BATCH-09.1 (Corrective)  
**Tasks:** Complete Task 0 from BATCH-09 (Golden Rig Verification)  
**Phase:** Stage 1 - Verification (Cyclone DDS Interop)  
**Estimated Effort:** 3-4 hours  
**Priority:** CRITICAL (blocking for production use)  
**Dependencies:** BATCH-09 (union implementation complete)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This corrective batch completes the Golden Rig verification that was skipped in BATCH-09. You will write and execute **actual C test programs** to verify byte-perfect compatibility with Cyclone DDS.

**Your Mission:**  
Verify that C# union serialization produces **identical bytes** to Cyclone DDS C serialization AND verify forward/backward compatibility behavior.

**Critical Context:**
- BATCH-09 implementation is based on opcode analysis (indirect)
- We need **actual hex dumps** of serialized bytes
- Must verify **forward/backward compatibility** (adding arms, adding fields)
- Must prove **C-to-C# and C#-to-C interop** works

---

## 🗂 File and Tool Locations ***(EXPLICIT PATHS)***

**⚠️ CRITICAL: Use these exact paths**

### Cyclone DDS Tools

**idlc (IDL Compiler):**
```
Primary location: d:\Work\CycloneDDS\build\bin\idlc.exe
Fallback: Run `where idlc` in terminal
If not found: Check d:\Work\CycloneDDS\build\bin\ or d:\CycloneDDS\bin\
```

**ddsc.dll (Runtime Library):**
```
Primary location: d:\Work\CycloneDDS\build\bin\ddsc.dll
Also need: d:\Work\CycloneDDS\build\lib\ddsc.lib (for linking)
```

**Include Path:**
```
d:\Work\CycloneDDS\build\include\
d:\Work\CycloneDDS\src\core\ddsc\include\
```

### Project Locations

**Golden Rig Tests:**
```
Project: d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Core.Tests
Existing tests: GoldenConsistencyTests.cs
```

**Working Directory for This Batch:**
```
d:\Work\FastCycloneDdsCsharpBindings\tests\GoldenRig_Union\
(You will create this directory)
```

### Compiler

**Visual Studio Developer Command Prompt:**
```
Start Menu → Visual Studio 2022 → Developer Command Prompt for VS 2022
OR
Run: "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
```

---

## Context

**BATCH-09 Status:** Union implementation complete, 111/111 tests passing.

**What's Missing:** Actual C serialization verification and compatibility testing.

**This Batch:** Execute comprehensive Golden Rig tests with actual C code.

---

## 🎯 Batch Objectives

**Primary Goal:** Verify byte-perfect compatibility and forward/backward compat with Cyclone DDS C code.

**Success Metrics:**
- C test programs compile and run
- Hex dumps captured for all test cases
- C#-to-C and C-to-C# produce identical bytes
- Forward/backward compatibility behavior documented
- Findings documented in report

---

## ✅ Task 0.1: Basic Union Serialization Hex Dump

**Goal:** Get actual hex dump of Cyclone C union serialization.

### Step-by-Step Instructions

**1. Create working directory:**
```cmd
cd d:\Work\FastCycloneDdsCsharpBindings\tests
mkdir GoldenRig_Union
cd GoldenRig_Union
```

**2. Create IDL file:**

Save as: `d:\Work\FastCycloneDdsCsharpBindings\tests\GoldenRig_Union\UnionTest.idl`

```idl
@appendable
union TestUnion switch(long) {
    case 1: long valueA;
    case 2: double valueB;
};

@appendable
struct Container {
    TestUnion u;
};
```

**3. Generate C code:**

```cmd
d:\Work\CycloneDDS\build\bin\idlc.exe -l c UnionTest.idl
```

**Expected output:**
- `UnionTest.c`
- `UnionTest.h`

**If idlc fails with "not found":**
```cmd
where idlc
# If not found, search:
dir d:\Work\CycloneDDS\build\bin\idlc.exe
dir d:\CycloneDDS\bin\idlc.exe
```

**4. Create C test program:**

Save as: `d:\Work\FastCycloneDdsCsharpBindings\tests\GoldenRig_Union\test_union_basic.c`

```c
#include "UnionTest.h"
#include <stdio.h>
#include <string.h>
#include "dds/dds.h"

void print_hex(const char* label, const unsigned char* data, size_t len) {
    printf("%s (%zu bytes):\n", label, len);
    for (size_t i = 0; i < len; i++) {
        printf("%02X ", data[i]);
        if ((i + 1) % 16 == 0) printf("\n");
    }
    if (len % 16 != 0) printf("\n");
    printf("\n");
}

int main() {
    Container c;
    unsigned char buffer[256];
    
    printf("=== Test Case 1: Union with case 1 (long) ===\n");
    c.u._d = 1;
    c.u._u.valueA = 0x12345678;
    
    // Serialize using Cyclone's generated code
    dds_ostream_t os;
    dds_ostream_init(&os, buffer, sizeof(buffer), 0);
    
    // Call generated serializer
    TestUnion__alloc *u_ptr = &c.u;
    Container__alloc *c_ptr = &c;
    
    // Serialize (function name may vary, check UnionTest.c)
    Container_serialize(c_ptr, &os);
    
    size_t size = dds_ostream_written(&os);
    
    print_hex("Serialized bytes", buffer, size);
    
    printf("=== Analysis ===\n");
    if (size == 12) {
        printf("Size is 12 bytes: NO Union DHEADER\n");
        printf("Structure: [Container DHEADER: 4] [Disc: 4] [ValueA: 4]\n");
    } else if (size == 16) {
        printf("Size is 16 bytes: HAS Union DHEADER\n");
        printf("Structure: [Container DHEADER: 4] [Union DHEADER: 4] [Disc: 4] [ValueA: 4]\n");
    } else {
        printf("Unexpected size: %zu bytes\n", size);
    }
    
    printf("\n=== Test Case 2: Union with case 2 (double) ===\n");
    c.u._d = 2;
    c.u._u.valueB = 3.14159;
    
    dds_ostream_init(&os, buffer, sizeof(buffer), 0);
    Container_serialize(c_ptr, &os);
    size = dds_ostream_written(&os);
    
    print_hex("Serialized bytes", buffer, size);
    
    return 0;
}
```

**5. Compile:**

**Open Developer Command Prompt for VS 2022**, then:

```cmd
cd d:\Work\FastCycloneDdsCsharpBindings\tests\GoldenRig_Union

cl /I"d:\Work\CycloneDDS\build\include" ^
   /I"d:\Work\CycloneDDS\src\core\ddsc\include" ^
   test_union_basic.c UnionTest.c ^
   /link /LIBPATH:"d:\Work\CycloneDDS\build\lib" ddsc.lib ^
   /OUT:test_union_basic.exe
```

**If compile fails:**
- Check paths exist: `dir d:\Work\CycloneDDS\build\include`
- Check ddsc.lib: `dir d:\Work\CycloneDDS\build\lib\ddsc.lib`
- Adjust paths as needed

**6. Run:**

```cmd
set PATH=%PATH%;d:\Work\CycloneDDS\build\bin
test_union_basic.exe
```

**7. Capture output:**

Copy console output to report. Screenshot recommended.

### Deliverables

- ✅ `UnionTest.idl`
- ✅ Generated `UnionTest.c`, `UnionTest.h`
- ✅ `test_union_basic.c`
- ✅ `test_union_basic.exe` (compiled)
- ✅ **Console output with hex dump**
- ✅ **Screenshot or text file of output**

---

## ✅ Task 0.2: Forward Compatibility Test (Add New Arm)

**Goal:** Verify old C# reader can handle new C union arm.

### Step-by-Step Instructions

**1. Create "old" IDL (2 cases):**

Save as: `d:\Work\FastCycloneDdsCsharpBindings\tests\GoldenRig_Union\UnionOld.idl`

```idl
@appendable
union TestUnion switch(long) {
    case 1: long valueA;
    case 2: double valueB;
};

@appendable
struct Container {
    TestUnion u;
};
```

**2. Create "new" IDL (3 cases - added case 3):**

Save as: `d:\Work\FastCycloneDdsCsharpBindings\tests\GoldenRig_Union\UnionNew.idl`

```idl
@appendable
union TestUnion switch(long) {
    case 1: long valueA;
    case 2: double valueB;
    case 3: string valueC;  // NEW ARM
};

@appendable
struct Container {
    TestUnion u;
};
```

**3. Generate C code for NEW version:**

```cmd
cd d:\Work\FastCycloneDdsCsharpBindings\tests\GoldenRig_Union
d:\Work\CycloneDDS\build\bin\idlc.exe -l c -o UnionNew UnionNew.idl
```

This creates: `UnionNew.c`, `UnionNew.h`

**4. Create C test (NEW publisher):**

Save as: `test_forward_compat.c`

```c
#include "UnionNew.h"
#include <stdio.h>
#include "dds/dds.h"

void print_hex(const char* label, const unsigned char* data, size_t len) {
    printf("%s (%zu bytes):\n", label, len);
    for (size_t i = 0; i < len; i++) {
        printf("%02X ", data[i]);
        if ((i + 1) % 16 == 0) printf("\n");
    }
    if (len % 16 != 0) printf("\n");
    printf("\n");
}

int main() {
    Container c;
    unsigned char buffer[256];
    
    printf("=== NEW Publisher: Sending case 3 (unknown to OLD readers) ===\n");
    c.u._d = 3;
    c.u._u.valueC = "Hello";
    
    dds_ostream_t os;
    dds_ostream_init(&os, buffer, sizeof(buffer), 0);
    Container_serialize(&c, &os);
    size_t size = dds_ostream_written(&os);
    
    print_hex("Serialized bytes (with case 3)", buffer, size);
    
    printf("=== Expected Behavior for OLD Reader ===\n");
    printf("1. Reads Container DHEADER\n");
    printf("2. Reads Union DHEADER\n");
    printf("3. Reads Discriminator: 3\n");
    printf("4. Discriminator 3 is UNKNOWN\n");
    printf("5. Uses Union DHEADER to SKIP to end\n");
    printf("6. Stream sync maintained\\n");
    
    return 0;
}
```

**5. Compile and run:**

```cmd
cl /I"d:\Work\CycloneDDS\build\include" test_forward_compat.c UnionNew.c /link /LIBPATH:"d:\Work\CycloneDDS\build\lib" ddsc.lib /OUT:test_forward_compat.exe

set PATH=%PATH%;d:\Work\CycloneDDS\build\bin
test_forward_compat.exe
```

**6. Manually test OLD C# reader:**

In C#, use your BATCH-09 deserializer on the hex dump from step 5.

**Expected:** C# reader should:
1. Read DHEADER
2. See discriminator = 3
3. Hit `default` case in switch
4. Skip to endPos
5. **Not crash, continue reading**

### Deliverables

- ✅ Console output showing case 3 hex dump
- ✅ Manual verification that C# deserializer handles it

---

## ✅ Task 0.3: C#-to-C Interop Verification

**Goal:** Verify C# serialization matches C byte-for-byte.

### Step-by-Step Instructions

**1. C# serialization:**

Write a manual C# test (or extend existing UnionTests.cs):

```csharp
// In tests/CycloneDDS.CodeGen.Tests or manual test
var testUnion = new TestUnion { Kind = 1, ValueA = 0x12345678 };
var container = new Container { U = testUnion };

var writer = new ArrayBufferWriter<byte>();
var cdr = new CdrWriter(writer);
container.Serialize(ref cdr);
cdr.Complete();

byte[] csharpBytes = writer.WrittenSpan.ToArray();
string csharpHex = BitConverter.ToString(csharpBytes);

Console.WriteLine($"C# Hex: {csharpHex}");
```

Run this and capture the hex string.

**2. Compare to C hex dump from Task 0.1:**

- **C bytes:** From `test_union_basic.exe` output
- **C# bytes:** From above C# test

**3. Verify byte-for-byte match:**

```
C  Hex: 0C 00 00 00 08 00 00 00 01 00 00 00 78 56 34 12
C# Hex: 0C-00-00-00-08-00-00-00-01-00-00-00-78-56-34-12

Match: YES/NO
```

### Deliverables

- ✅ C# hex output
- ✅ C hex output (from Task 0.1)
- ✅ **Comparison result: MATCH or MISMATCH**

---

## 📊 Report Requirements

**Submit to:** `.dev-workstream/reports/BATCH-09.1-REPORT.md`

**Required Sections:**

1. **Task 0.1: Basic Hex Dump**
   - **MUST INCLUDE:** Full console output from `test_union_basic.exe`
   - **MUST INCLUDE:** Hex dump for case 1 (long)
   - **MUST INCLUDE:** Hex dump for case 2 (double)
   - **MUST INCLUDE:** Size in bytes
   - **DECISION:** DHEADER present or not?

2. **Task 0.2: Forward Compatibility**
   - **MUST INCLUDE:** Hex dump of case 3 (new arm)
   - **MUST INCLUDE:** Verification that C# deserializer handles unknown arm
   - Does C# skip correctly using DHEADER?

3. **Task 0.3: C#-to-C Interop**
   - **MUST INCLUDE:** C# hex output
   - **MUST INCLUDE:** C hex output
   - **MUST INCLUDE:** Byte-for-byte comparison
   - **RESULT:** MATCH or MISMATCH (if mismatch, explain)

4. **Findings Summary**
   - Union wire format confirmed
   - Forward compatibility confirmed (yes/no)
   - C# implementation matches C (yes/no)
   - Any discrepancies requiring fixes

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ All C test programs compile and run
- ✅ Hex dumps captured for all test cases
- ✅ C#-to-C byte match verified
- ✅ Forward compatibility behavior verified
- ✅ Findings documented with screenshots/output
- ✅ Report submitted

**If any mismatch found:** Document and recommend fixes to BATCH-09 implementation.

---

## ⚠️ Common Pitfalls to Avoid

1. **idlc not found:**
   - Use full path: `d:\Work\CycloneDDS\build\bin\idlc.exe`
   - If still not found, search entire d:\Work\ drive

2. **Compilation errors:**
   - Verify include paths exist
   - Verify ddsc.lib exists
   - Use Developer Command Prompt for VS 2022

3. **Runtime errors (DLL not found):**
   - Add to PATH: `set PATH=%PATH%;d:\Work\CycloneDDS\build\bin`

4. **Forgetting to capture output:**
   - Screenshot OR redirect: `test_union_basic.exe > output.txt`

---

## 📚 Reference Materials

- **Cyclone DDS Docs:** Check `d:\Work\CycloneDDS\docs\`
- **BATCH-09 Implementation:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- **UnionTests:** `tests/CycloneDDS.CodeGen.Tests/UnionTests.cs`

---

**Estimated Time:** 3-4 hours (including troubleshooting tool paths)
