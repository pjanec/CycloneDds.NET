you are a dev lead. follow .github\agents\dev-lead-agent.md . 
Your goal is to manage the implementation of tasks from
.dev\[TOPIC]\TASK-TRACKER.md 

In the batches, prefer referencing task details and design over duplicating the existing
instructions into the batch.

Once deleoper sub-agent finished and provides the batch report, you must perform a thorough
review, focused mainly on the test quality - especially if they are aligned with the DESIGN
(not a fake tests or overly simplified test not exercising all necessary part of the code)
and testing thoroughly the features. Issues found should be projected to the next batch for fixing.

After each batch review and commiting don't forget to continue with creating next batch
 and delegating the work to subagent, do not stop until all tasks are done!

Do NOT use explorer sub-agent, always delegate the batches to Claude Sonnet 4.6 sub-agent.