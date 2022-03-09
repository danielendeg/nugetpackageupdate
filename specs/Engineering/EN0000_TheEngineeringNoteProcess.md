# EN0000 The Engineering Note Process

Orignally authored by Ed Nightingale, Jan 2017.
Generalized and updated for hybrid work environments by Adrian Bonar, March 2022.

## Goals of this Engineering Note

This engineering note provides guidance on the why, how and when to author an
engineering note. The note will also provide guidance on why engineering notes
are important, a discussion of design and implementation workflow when working
within the agile methodology and how this process may be used to avoid design
pitfalls the team has run into during the delivery of Technical Preview 1.
Finally, the goal of this engineering note is to help accelerate a process of
thoughtful design that will result in the delivery of a better end-product.

One should also note the goals of using engineering notes: to encourage a process
of investigation, design, and implementation that is principled rather than ad-hoc,
thoughtful rather than hurried, and one that improves the probability of being
right the first time an implementation is considered complete.

An Engineering Note has a lifecycle, from draft to approval to maintenance to
obsolescense. Once approved, it should be treated as a living document, via
addenda. It may also be flagged as obsolete if it ages out (and moved to a
new location, to avoid clutter), or if it is supplanted by a new EN.

## Non-Goals of this Engineering Note

This Engineering Note is *not* a checklist. If engineering notes are treated as checklists,
then we will fail as a team to strike the right balance between our ability to ship
quickly and ensuring our product contains designs and implementations that are
thoughtful, maintainable, secure and correct. Similarly, this engineering note is
not an algorithm. It cannot be blindly followed to produce good design.

## Problem Summary

Modern hybrid development teams are spread across continents time zones. Our projects 
have ambitious goals and ambitious timelines. Delivering a final product requires 
teams to solve complicated design problems that span people, workgroups, and continents.
Time pressure, time zones, and large numbers of problems to solve encourage an 
ethos to simply ready, fire, aim. This pattern of work often results in designs that 
may not meet product requirements, or worse, designs and implementation that do something 
without it becoming obvious until afterwards whether that something is *“the right thing”*
for our customers or our product. Therefore, we desire a process that helps up-level 
the team in meeting product requirements (through architecture, design, and implementation) 
and therefore avoid wasting time and effort building the wrong software, or worse,
building software at the wrong quality bar. Such efforts create debt that ultimately 
puts the entire product at risk.

## Solution Space

There is a definite tension between design and implementation progress. At one end
of the spectrum is the much maligned “waterfall,” where the entire product is
planned in one go, and then all implementation begins. At the other end, is a
process where teams fluidly and “on-demand” implement features as they appear to be
important. Both ends of the spectrum have trade-offs and downsides. Note that
each end of the spectrum has its place. Software written as part of the Apollo
space program was sometimes written in a waterfall process. In fact, every feature
and every function was not only specified, but also written in pseudo-code and
reviewed before any actual code was written. This costly process was viewed as
required for their problem area (space flight). Process also depends on team
composition, as teams grow and the variance in design experience widens, it becomes
more important to introduce process that encourages better design. Likewise, a
completely fluid process that depends upon everyone simply “doing the right thing”
tends towards a more chaotic design – when teams are separated by space, or when
teams are undergoing periods of growth this process tends to break down. It
becomes far more difficult for new employees or employees separated by distance to
ensure their feedback is heard and whether designs meet product requirements

## Proposed Solution

The engineering note process strives to strike a balance between thinking, writing,
designing and coding. Ideally, most of a developer’s time is spent thinking (about
design and implementation) and coding, with a minority of time spent on writing and
revisions. 

Note that this flow should be considered by product requirement, not for the entire
product. Each requirement should be at a different point in its lifecycle during
any given sprint. Some product requirements will be in active implementation, while
others will be in a design, or architectural requirements phase. Note also that
this model does not require “stop the world” while an EN is written. If the design
and architectural requirements are not well understood, i.e., “we don’t know how to
do this,” then a period of experimentation, prototyping, or learning may occur.
Once that period is complete, an EN may be written with some clarity covering
architectural requirements, design requirements, or both. If everything is already
clear, an initial EN might be written summarizing a planned design. After the
shipping implementation is complete, the author will return and update the EN
reflecting any changes made during the implementation of the product requirement.

Before any architectural requirements are written, and before *any* experimentation,
two questions must be answered: first, is the product definition well understood?
In other words, do we know what we’re delivering and why?  Second, is the individual
product requirement well understood or well defined?  If the answer to either of
these questions is “no,” then work must stop. Missing product requirements, or not
understanding product requirements, are events that must involve program management.
If product requirements are rich enough, an EN might even be written to describe
the requirements and why they were chosen. Otherwise, ENs may be written for
architectural requirements, or high-level designs. Low-level design work should
typically not be reflected in an EN. Stories might provide guidelines of what a
developer is planning on implementing but ultimately functions, methods, and tests
best describe an implementation.

A final note on process – this updated EN model encourages a lightweight process, 
and it’s ok to have short ENs (a paragraph or few sentences per section), if the 
writing meets the goals listed in the EN. Please see the EN template, which has 
been updated with minimum expected sections. Note that it has been streamlined 
and simplified to make the process easier to follow.

## We Know We Need an EN, Now What?

1. Identify the stakeholders for a feature, leveraging the [OARP Model for Decision-Making for ENs](OARPModel.md). 
If you’re not sure who the stakeholders are, ask a lead or member of the engineering
leadership team. Stakeholders are generally a small group that are directly dependent
on a feature. If you’ve identified many stakeholders, it’s probably too big a group.

2. Schedule a meeting and hash out the design or architecture. Don’t spend energy 
writing up a document or email thread. Save cycles.

3. If the group decides it’s unclear what to do, schedule experimentation and prototype
stories into the sprint.

4. Elect one person to write up the results into an EN in markdown, in the `health-paas-docs` 
repo. Create a branch and a work-item for this EN, so you can iterate and fine-tune.
    - ADO's [Syntax guidance for basic Markdown usage](https://docs.microsoft.com/en-us/azure/devops/project/wiki/markdown-guidance?view=azure-devops) 
    has good documentation on how to write markdown, and VSCode has built-in markdown 
    support, including a preview pane.

    Share only with the stakeholders identified via OARP.

    Use a Pull Request (PR) to manage final signoff on the EN document. Include the
    approver(s) on the PR as Required, and the reviewers as Optional. The PR process
    should be uneventful given the pre-work ahead of this point. Once an EN has been 
    opened for review, reviewers are expected to provide feedback within a few days,
    though the sooner the better.As a reviewer, if you are unable to respond in that 
    time, either set expectations with the author, or remove yourself from the
    review process.

    Once approved, complete the PR, which will merge the change into the master
    branch. Then send out an announcement of the approval to an appropriate audience.

5. Implement the feature. If there are changes or new learning from the
implementation, update the EN via a branch and work item, and use the same OARP
process and Pull Request to manage the decision-making process.

    Consider informing program management so that someone knows the updates occurred.
