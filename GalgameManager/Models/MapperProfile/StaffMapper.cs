// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using System.Runtime.CompilerServices;

// LemonVN modification: preserve type forwarders; the unused mapping profile
// is removed. Existing staff synchronization uses explicit assignments.

[assembly: TypeForwardedTo(typeof(GalgameManager.Models.Staff))]
[assembly: TypeForwardedTo(typeof(GalgameManager.Models.StaffRelation))]
[assembly: TypeForwardedTo(typeof(GalgameManager.Models.StaffGame))]
[assembly: TypeForwardedTo(typeof(GalgameManager.Models.StaffIdentifier))]
