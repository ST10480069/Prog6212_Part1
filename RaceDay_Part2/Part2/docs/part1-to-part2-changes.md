# Changes from the Part 1 plan

Part 2 follows the Part 1 ERD, SQL script and endpoint plan. These are the places where the Part 2 brief
asked for something Part 1 did not cover, so the design was extended. **The Part 1 ERD image in `docs/`
should be updated to show the new columns** (marked below).

## Schema (ERD + SQL script)

| Table | Change | Why |
|---|---|---|
| `Event` | **+ `DistanceKm`** `DECIMAL(5,2) NOT NULL`, must be > 0 | Brief: each event captures a distance |
| `Event` | **+ `EventType`** `NVARCHAR(10) NOT NULL`, one of `Run`, `Walk`, `Cycle` | Brief: each event captures an event type |
| `Category` | `DistanceKm` is now **nullable** | Age categories (e.g. "Senior 50+") have no distance |
| `Category` | **+ `CategoryType`** `NVARCHAR(10) NOT NULL`, `Distance` or `Age` | Brief: categories can be age or distance |
| `Category` | **+ `MinAge`**, **+ `MaxAge`** `INT NULL` | Age range for age categories |
| `Category` | **+ check `CK_Category_Details`**: Distance needs `DistanceKm`, Age needs `MinAge` | Keeps the data consistent |

Everything else (Role, User, Enrolment, Result and all relationships) is unchanged.
`docs/raceday_schema_part2.sql` is the full updated script.

## Endpoint plan

| Change | Why |
|---|---|
| `POST /api/auth/login` starts a **session** (cookie) and returns the profile, instead of returning a token | Brief requires session management |
| New `POST /api/auth/logout` | Ends the session (needed to switch users in Swagger) |
| `POST/PUT /api/events` bodies also include `distanceKm` and `eventType` | New Event columns |
| `POST/PUT` category bodies also accept `categoryType`, `minAge`, `maxAge` | New Category columns |
| `POST /api/auth/register` accepts `organiserCode` for Organiser accounts | Brief: "think about how role selection should be managed" - without a code anyone could make themselves an Organiser |
| `GET /api/events` returns non-draft events; `Draft` events are only visible to their owner | Plan said "published events" |
| Extra `409 Conflict` cases: deleting an event/category that has enrolments; lowering a category's capacity below its confirmed enrolments; enrolling in a second category of the same event; enrolling in an event that is not Published; recording a position already used in the category; recording a result for a cancelled enrolment | Protects data integrity |
| `DELETE /api/enrolments/{id}` cancels (sets `Status = Cancelled`) rather than removing the row | Keeps history; frees the place |
