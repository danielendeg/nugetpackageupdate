# The OARP Model
## Owner:
Owns driving the decision and process.  Responsible for delivering.
* Confirms ownership with Approver​
* Agrees with Approver on scope of decision, Reviewers, accuracy vs. speediness​
* Uses input received to formulate recommendation​
* Seeks Reviewers’ input on recommendation; incorporate or show Reviewers disagreements
* Obtains agreement of Approver
* Informs Participants and affected employees of decision to get buy-in, before executing
* Requests coaching assistance as needed
* Drives post-mortem of the process, capturing new topics of learning

## Approver:
Makes the final sign-off on the decision.  Accountable for the decision.  There may be more than 1 approver, but this should be minimized.
* May initiate process, identifying needed decision and delegating to Owner​
* Heavily involved with initial scoping, contracting​
* Intervene if progress is stalled​
* Considers Owner’s recommendations and approves, adjusts, or rejects; may involve other levels of hierarchy 
* Handles objections from Reviewers​

## Reviewer:
Provides feedback to the Owner and Approver.  Subject matter experts that are consulted for their experience. Selected by Owner & Approver because they will be impacted​ by the recommendation.
* Must commit to be involved, or they abdicate role
* Cannot veto a decision, but can present objections to Approver​s

## Participant:
Contributes to the effort.  May be empty for smaller decisions.
* Input providers named by Owner or self-identified​
* Presents views to be considered by Owner
* For decisions with large numbers of interested people, representatives are selected​

## **(Optional)** Inform:
**This is optional and not needed for most OARP decisions.**  People who need to know about the decision, either as it is worked on, or when it is approved.  But they are read-only, not participating in the decision.

---
# See also
- Engineering Excellence [Decision Making using the OARP Model](https://microsoft.sharepoint.com/:p:/r/teams/SecDev/_layouts/15/Doc.aspx?sourcedoc=%7B0F95B34A-2CBF-4250-AD24-E38781220C25%7D&file=Decision%20Making%20using%20the%20OARP%20Model.pptx&action=edit&mobileredirect=true&DefaultItemOpen=1&CID=A95932A4-B368-42F7-A4EE-6B810805D808&wdLOR=c4AEC30C0-6DD8-4CCA-8412-2754DB0A536C)
  - the Accountability Checklist on slide 9 can help clarify Owner vs Approver
- stumblingabout.com [Ownership, roles and responsibilities](https://stumblingabout.com/2011/01/27/ownership-roles-and-responsibilities/)

---
# Questions?
Please contact - in person, on Teams, by email:
- Adrian Bonar (adribona) - Sponsor and pilot

---
# OARP Applied to Engineering Processes
##EN (Engineering Note)
>O - EN author
A - Key stakeholders - architect-level people
R - Subject matter experts.  Risk of overlap with 'A', so Reviewers may be empty to start
P - Might be empty

##RFC (Request for Comments)
>O - RFC author
A - Subsystem owner.  Goal is a single approver.. split if > 1
R - Subsystem owner delegates, plus Subject Matter Experts
P - Contributing to the decision - more useful for a large multi-person RFC.  Consider adding the engineers who will be implementing the change.

##PR (Pull Request)
>O - PR author 
A - Code maintainer (eventually automated based on maintainers.txt).  Accountable.
R - Discretion of the owner - people with historical context.  Approvers can recommend additional reviewers because they'll have wisdom.  Maybe empty. Consultant.
P - Contributing to the decision - more useful for a large multi-person PR

---
# OARP Examples
## Example 1
Agartha Team Vision & Direction

>O - Ryan, Michele - own the proposals and driving them to completion
A - Ed, Halina - own making sure decisions line up with business needs
R - Workstream leads - provide feedback on impact and direction based on their expertise and complementary areas of ownership
P - Agartha team IC members - contribute to the plan, execute on it

## Example 2
Adding watchdogs to the platform - done by the platform team to help load-balance
>O - Matt - IC driving the work
A - Ryan - Platform architect
R - Alan, Jeff - Platform Subject Matter Experts
P - none - no other ICs involved, given the scope

