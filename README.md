# Prog6212_Part1
descriptions of the system
RaceDay is an event management system for organising and participating in running/race events. Organisers can create events, define race categories within each event (e.g. 5km, 10km, Half Marathon), and record results once races are complete. Participants can browse published events, enrol in a specific category (which assigns them a bib number), track their own enrolments, and view their results after racing. The system is built around a role-based data model, where every user is either an Organiser or a Participant, and access to each action is restricted accordingly — Organisers manage the events they own, Participants manage their own enrolments.
descriptions
Organiser
An Organiser creates and manages race events. They can create, update, and delete their own events, add race categories to those events (e.g. distance, price, capacity), view who has enrolled in each category, and record participants' results once a race has taken place. Organisers can only manage events, categories, and results that they own — not other Organisers' events.
Participant
A Participant browses published events and enrols in a category of their choice, which assigns them a bib number. They can view their own list of enrolments, cancel an enrolment if needed, and view their own results once they've raced. Participants cannot create events or record results — only Organisers can do that.
<img width="1082" height="135" alt="image" src="https://github.com/user-attachments/assets/9391e0a0-dda7-4048-8c8d-76b7feb20f94" />
