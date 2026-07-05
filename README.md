# MailArchive System
### !!!!!!!!!!!!!!!!!!!!!!README OUTDATED!!!!!!!!!!!!!!!!!!!!!!!!

Backend σύστημα για διαχείριση mailboxes με υποστήριξη χρηστών, pagination και δομημένων API responses. Υλοποιημένο με ASP.NET Core, EF Core και PostgreSQL.

---

## Τρέχουσα κατάσταση

Το project αυτή τη στιγμή περιλαμβάνει:

- Διαχείριση χρηστών και mailboxes
- Pagination στη λίστα mailboxes
- Ενιαία μορφή API responses
- PostgreSQL βάση δεδομένων με EF Core migrations
- Seeded test data για άμεσο testing

---

## Τεχνολογίες

- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- Swagger
- C#

---

## Εκτέλεση του project

### Βάση δεδομένων
Βεβαιώσου ότι η PostgreSQL τρέχει και ότι το connection string είναι σωστά ρυθμισμένο στο:

MailArchive.API/appsettings.json

### Εκκίνηση API

```bash
dotnet run --project MailArchive.API
```



### Τρόπος δοκιμής

Users
GET:
/api/users

GET by id:
/api/users/{id}

POST:
/api/users

Mailboxes

GET (pagination):
/api/mailboxes?page=1&pageSize=10

GET (search):
/api/mailboxes?page=1&pageSize=10&search=Main

GET by id:
/api/mailboxes/{id}

POST:
/api/mailboxes

PUT:
/api/mailboxes/{id}


### Response format

Όλα τα endpoints επιστρέφουν ενιαίο format:

{
  "data": {},
  "isSuccess": true,
  "error": null
}

Για pagination:

{
  "data": {
    "items": [],
    "totalCount": 0,
    "page": 1,
    "pageSize": 10
  },
  "isSuccess": true,
  "error": null
}
