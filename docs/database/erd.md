# ERD

This database supports the approved blood-inventory workflow without donor data. The canonical entity model is centered on facilities, internal blood needs, inter-facility blood requests, inventory records, and auditing.

## Relationship summary

- A facility owns inventory, staff assignments, needs, and request records.
- Each blood inventory row is unique to one facility and one blood type.
- Each request is linked to a need, a requesting facility, and a source facility.
- Each request has a status history timeline that is immutable after creation.
- Inventory transactions log every stock and reservation change.
- Notifications and audit logs reference users without becoming the source of truth for the business workflow.

## Mermaid diagram

See [erd.mmd](erd.mmd) for the source diagram.
