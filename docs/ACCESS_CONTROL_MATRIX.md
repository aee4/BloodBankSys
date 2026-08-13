# Access Control Matrix

| Capability | SystemAdmin | FacilityAdmin | Hospital staff | Blood-bank staff |
| --- | --- | --- | --- | --- |
| Register a new facility | No | Public onboarding creates pending facility and first admin | No | No |
| Approve, reject, suspend, restore facility | Yes | No | No | No |
| Manage own facility profile | View/support | Yes | View | View |
| Create or deactivate own staff | No by default | Yes | No | No |
| View own facility inventory | Platform oversight only | Yes | Yes | Yes |
| Adjust own facility inventory | No by default | Yes | No | No |
| Create internal blood need | No | No, reviews submitted needs | Yes | Yes |
| View all own-facility needs | Oversight only | Yes | Own submissions only | Own submissions only |
| Search network availability | No by default | Yes | No | No |
| Create external request | No | Yes | No | No |
| Accept, reject, fulfil received request | No | Yes for own facility | No | No |
| View notifications | Own | Own | Own | Own |
| View platform audit/reporting | Yes | Own-facility summary | No | No |

FacilityStaff permissions are the same whether the facility is a hospital or a blood bank. FacilityAdmin users do not receive platform authority.

Every protected service operation must verify signed-in user, role, active status, FacilityId, approved facility status, and record relationship.
