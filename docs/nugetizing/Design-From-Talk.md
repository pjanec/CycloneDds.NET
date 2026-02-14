Read the cumulative design talk document till the end to get the final idea.
I mean it! the document is long and you MUST read it till the last line, otherwise you will not understand all ideas!
READ IT TILL THE END, all lines!
Check the current implementation of the system in the aspects we are going to refactor to understand what the design talk refers to.


DESIGN DOCUMENT

Create a detailed design document docs/DESIGN.md capturing all the final ideas from the design talk document.
Break the design into implementation phases and tasks.



TASK DETAILS DOCUMENT

Create task detail document docs/TASK-DETAIL.md containing the detailed description of every task.
Each task needs a unique id and clearly in detail stated success conditions - usually a specification of unit tests.
The task detail document should reference the chapters of the design document to avoud duplicating information.
The task detail description together with design document should provide complete understanding for the developer.



TASK TRACKER DOCUMENT

Create also a task tracker document, listing the phases and tasks in brief, with binary status indicator for each task (not done/done).
The task traker should reference the task details document in its header.
For each task there should be a link to corresponding chapter of the task detail document.
See the task tracker sample format below in this document.



ONBOARDING INSTRUCTIONS

Create ./ONBOARDING.md file with instructions for a newcomers. The document should give the new developer an overview of the
project (what stuff we are building/refactoring in the detailed design), where are the design and task documents,  mention were in the folder layout there are the components
we need (those refactored, or these required for taking stuff from..), how to build the project etc.

It should also contain instruction to read the DEV-GUIDE.md document defining how the developer should behave.




Example of a task tracker follows:

# Task Tracker Sample

**Reference:** See [TASK-DETAILS.md](./TASK-DETAILS.md) for detailed task descriptions


## Stage 1: Foundation - CDR Core

**Goal:** Build and validate CDR serialization primitives before code generation  

- [x] **FCDC-S001** Core Package Setup [details](./TASK-DETAILS.md#fcdc-s001-cycloneddscore-package-setup)
- [x] **FCDC-S002** CdrWriter Implementation [details](../docs/SERDATA-TASK-MASTER.md#fcdc-s002-cdrwriter-implementation)
- [ ] **FCDC-S003** CdrReader Implementation [details](../docs/SERDATA-TASK-MASTER.md#fcdc-s003-cdrreader-implementation) 
- [x] **FCDC-S004** AlignmentMath + CdrSizer [details](../docs/SERDATA-TASK-MASTER.md#fcdc-s004-cdrsizecalculator-utilities)
- [x] **FCDC-S005**  Golden Rig Validation (GATE) [details](../docs/SERDATA-TASK-MASTER.md#fcdc-s005-golden-rig-integration-test-validation-gate)




## Stage 2: Code Generation - Serializer Emitter

**Goal:** Generate XCDR2-compliant serialization code from C# schemas  

- [ ] **FCDC-S006** Schema Package Migration [details](../docs/SERDATA-TASK-MASTER.md#fcdc-s006-schema-package-migration)
- [ ] **FCDC-S007** CLI Tool Generator Infrastructure [details](../docs/SERDATA-TASK-MASTER.md#fcdc-s007-cli-tool-generator-infrastructure)
