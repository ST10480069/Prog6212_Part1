# RaceDay - Part 2: RESTful API

ASP.NET Core 8 Web API for the RaceDay event system. It lets **Organisers** create race events (with categories and results) and lets **Participants** enrol in events and see their results. Data is stored in SQL Server through Entity Framework Core (code-first), users log in with a **session**, and every endpoint is documented and testable in **Swagger**.

> Part 1 (ERD, SQL script, endpoint plan) is in the repository root and `docs/`. A few small, deliberate extensions to the Part 1 design are listed in [`docs/part1-to-part2-changes.md`](docs/part1-to-part2-changes.md).

| | |
|---|---|
| **YouTube demo (unlisted)** | _paste your link here_ |
| **CI/CD status** | see the screenshot below |

## Contents
1. [The two roles](#the-two-roles)
2. [Setup and run](#setup-and-run)
3. [Using Swagger](#using-swagger)
4. [Authentication and role-based access](#authentication-and-role-based-access)
5. [Endpoints](#endpoints)
6. [Project structure](#project-structure)
7. [Unit tests](#unit-tests)
8. [CI/CD with GitHub Actions](#cicd-with-github-actions)

## The two roles

**Organiser** - creates and runs events.
Can create, update and delete **their own** events; add, change and remove age or distance categories on those events; see everyone who has enrolled in them; and record or correct finish times and positions. An Organiser cannot touch another Organiser's events and cannot enrol in events.

**Participant** - takes part in events.
Can browse events, enrol in a category (the server assigns a bib number), see and cancel their own enrolments, and view their own results. A Participant cannot create or change events, categories or results.

Both roles can view and update their own profile, and anyone (even without logging in) can browse events, categories and event results.

## Setup and run

**You need:** Visual Studio 2022 with the *ASP.NET and web development* workload, the .NET 8 SDK, and SQL Server (the LocalDB that comes with Visual Studio is enough).

1. **Clone the repository** and open `Part2/RaceDay/RaceDay.sln` in Visual Studio. Make sure **RaceDay.Api** is the startup project.
2. **Check the connection string** in `Part2/RaceDay/RaceDay.Api/appsettings.json`. The default uses LocalDB and a database called `RaceDayApiDb`:
   ```
   Server=(localdb)\MSSQLLocalDB;Database=RaceDayApiDb;Trusted_Connection=True;TrustServerCertificate=True;
   ```
   If you use full SQL Server, change the `Server=` part to the name shown in SSMS.
3. **Create the first migration** (once). Open *Tools > NuGet Package Manager > Package Manager Console*, set *Default project* to `RaceDay.Api`, and run:
   ```
   Add-Migration InitialCreate
   ```
   This generates the `Migrations` folder from the code-first model. (Command line alternative: `dotnet ef migrations add InitialCreate --project RaceDay.Api`.)
4. **Run the API** with **F5** (or `dotnet run --project RaceDay.Api`). On start-up the API applies the migration, creating the database, and in Development it adds the sample data from the Part 1 script.
5. Your browser opens on the **Swagger UI** (`https://localhost:7141/swagger`).

> If you ran the app *before* step 3, the database was created without migration history. Run `Drop-Database` in the Package Manager Console, then do steps 3 and 4 again.

### Demo accounts (Development only)

All sample accounts use the password **`Password123!`**.

| Role | Email |
|---|---|
| Organiser | `john.smith@raceday.com` |
| Organiser | `sarah.lee@raceday.com` |
| Participant | `mike.johnson@example.com` |
| Participant | `emma.brown@example.com` |

To register a **new Organiser** you must also send the organiser invite code (`organiserCode`). The default is `RACEDAY-ORGANISER-2026` and is set in `appsettings.json` under `Registration:OrganiserCode`. New Participants need no code.

## Using Swagger

1. Open `/swagger`. Every endpoint is listed with its purpose, the data it expects and the responses it can return.
2. Expand **POST /api/auth/login**, click *Try it out*, enter an email and password from the table above, and *Execute*. The server answers `200` and sets a **session cookie**.
3. The browser now sends that cookie automatically, so you can try the protected endpoints. Logged in as an Organiser you can create events; logged in as a Participant the same call returns `403 Forbidden`.
4. Use **POST /api/auth/logout** to end the session and switch user.

## Authentication and role-based access

* **Registration** - `POST /api/auth/register` takes the user's details and the role they want (`Organiser` or `Participant`).
  * Anyone can register as a Participant.
  * Organiser accounts can create events and record results, so they also need the **organiser invite code**. Without it the API answers `403`. This stops random visitors from making themselves Organisers.
* **Password storage** - passwords are hashed with **BCrypt** (with a per-password salt) before saving. The plain password is never stored or returned.
* **Login and sessions** - `POST /api/auth/login` checks the email and password. On success the user's **id and role are stored in a server-side session**, and the browser keeps only a session cookie. Wrong email or password gives the same `401` message so attackers cannot discover which emails exist.
* **Protecting endpoints** - protected actions use `[SessionAuthorize]` (any logged-in user) or `[SessionAuthorize("Organiser")]` / `[SessionAuthorize("Participant")]` (one role only). The filter lives in `Auth/SessionAuth.cs`.
  * Not logged in -> **401 Unauthorized**
  * Logged in with the wrong role -> **403 Forbidden**
* **Ownership checks** - role is not enough: an Organiser can only change events, categories, enrolments and results that belong to **their own** events (otherwise `403`).
* **Server-side values** - an enrolment needs no request body. The participant comes from the session and the bib number is assigned by the server, so nobody can enrol as someone else or choose their own bib.

## Endpoints

Roles: **none** = public, **any** = any logged-in user. Failure codes shown are the most common ones; Swagger lists all of them.

| Method | Route | Role | Purpose | Success |
|---|---|---|---|---|
| POST | `/api/auth/register` | none | Register an Organiser or Participant | 201 (409 email in use, 403 bad organiser code) |
| POST | `/api/auth/login` | none | Log in and start a session | 200 (401 bad credentials) |
| POST | `/api/auth/logout` | any | End the session | 204 |
| GET | `/api/users/me` | any | View my profile | 200 |
| PUT | `/api/users/me` | any | Update my name and phone number | 200 |
| GET | `/api/events` | none | List published events (`?eventType=Run\|Walk\|Cycle`) | 200 |
| GET | `/api/events/{id}` | none | Event details | 200 (404) |
| POST | `/api/events` | Organiser | Create an event | 201 |
| PUT | `/api/events/{id}` | Organiser (owner) | Update an event | 200 (403, 404) |
| DELETE | `/api/events/{id}` | Organiser (owner) | Delete an event | 204 (403, 404, 409 has enrolments) |
| GET | `/api/events/{eventId}/categories` | none | List an event's categories | 200 (404) |
| POST | `/api/events/{eventId}/categories` | Organiser (owner) | Add an age or distance category | 201 (403, 404, 409 duplicate name) |
| PUT | `/api/categories/{id}` | Organiser (owner) | Update a category | 200 (403, 404) |
| DELETE | `/api/categories/{id}` | Organiser (owner) | Delete a category | 204 (403, 404, 409 has enrolments) |
| POST | `/api/categories/{categoryId}/enrolments` | Participant | Enrol in a category (bib assigned by server) | 201 (404, 409 already enrolled / full) |
| GET | `/api/users/me/enrolments` | Participant | My enrolments | 200 |
| GET | `/api/categories/{categoryId}/enrolments` | Organiser (owner) | Who is enrolled in a category | 200 (403, 404) |
| DELETE | `/api/enrolments/{id}` | Participant (owner) | Cancel my enrolment | 204 (403, 404) |
| POST | `/api/enrolments/{id}/results` | Organiser (owner) | Record a finish time and position | 201 (403, 404, 409 already recorded) |
| PUT | `/api/results/{id}` | Organiser (owner) | Correct a result | 200 (403, 404) |
| GET | `/api/events/{eventId}/results` | none | Results of an event | 200 (404) |
| GET | `/api/users/me/results` | Participant | My results across all events | 200 |

## Project structure

```
Part2/
├── README.md
├── docs/                         Updated SQL script and the list of changes from Part 1
└── RaceDay/
    ├── RaceDay.sln
    ├── RaceDay.Api/
    │   ├── Models/               Entities (Role, AppUser, Event, Category, Enrolment, Result) + constants
    │   ├── Data/                 RaceDayDbContext (EF Core mapping), design-time factory, DbSeeder
    │   ├── Dtos/                 Request bodies (with validation rules) and response records
    │   ├── Auth/                 SessionAuthorize filter (401 / 403)
    │   ├── Controllers/          Auth, Users, Events, Categories, Enrolments, Results
    │   ├── Migrations/           Created by Add-Migration (step 3 of the setup)
    │   └── Program.cs            Services, session, Swagger, database start-up
    └── RaceDay.Tests/            xUnit tests (run the real API in memory)
```

## Unit tests

The tests start the real API in memory with `WebApplicationFactory` and replace SQL Server with EF Core's in-memory database, so they need no database and behave the same on every machine. Each test registers its own users and data, and every logged-in user gets their own `HttpClient`, and therefore their own session cookie.

| Area | What is checked |
|---|---|
| **Registration and login** (`AuthTests`) | Participant and Organiser registration, organiser code required, duplicate email, bad role / email / short password, password stored as a BCrypt hash, login success and failure, logout, separate sessions per client |
| **Profile** (`ProfileTests`) | View and update own profile for both roles, `401` when logged out |
| **Event management** (`EventTests`) | Organisers create, update and delete their own events; anonymous gets `401`; Participants and other Organisers get `403`; validation and not-found cases; drafts hidden |
| **Categories** (`CategoryTests`) | Distance and age categories, validation, duplicates, ownership and role checks |
| **Enrolments** (`EnrolmentTests`) | Participants enrol, the enrolment links participant + event + category and gets a bib, duplicates, full categories, role rejections, Organisers see their own enrolments only, cancelling |
| **Results** (`ResultTests`) | Organisers record and correct results, role/ownership rejections, Participants see only their own results, event results are public |
| **Swagger** (`SwaggerTests`) | Every endpoint from the plan appears in the Swagger document |

Run them in Visual Studio with *Test > Run All Tests*, or from a terminal:

```
cd Part2/RaceDay
dotnet test
```

## CI/CD with GitHub Actions

`.github/workflows/part2-ci.yml` (in the repository root) restores, builds and runs all tests on **every push** and pull request.

_Add a screenshot of a green run here (Actions tab -> the latest run), saved as `Part2/docs/ci-green-build.png`:_

![Successful GitHub Actions build](docs/ci-green-build.png)
