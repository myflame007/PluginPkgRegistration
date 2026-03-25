// ═══════════════════════════════════════════════════════════════════════
//  BEISPIEL: Plugin Step Registration
//  
//  So sieht ein typisches Plugin aus, das unser Tool automatisch
//  in Dataverse registriert. Die Attribute werden per MetadataLoadContext
//  gelesen — kein manuelles Registrieren mehr nötig.
//
//  ⚠️ Diese Datei ist NUR ein Beispiel und wird NICHT kompiliert.
//     (Fehlende Referenzen zu Microsoft.Xrm.Sdk etc.)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Xrm.Sdk;
using System;

namespace MyProject.Plugins
{
    // ─── Einfacher Plugin Step ─────────────────────────────────────
    //
    // CrmPluginRegistration(
    //   message,              → "Create", "Update", "Delete", etc.
    //   entity,               → Logischer Name der Entity
    //   stage,                → StageEnum: PreValidation=10, PreOperation=20, PostOperation=40
    //   executionMode,        → ExecutionModeEnum: Asynchronous=0, Synchronous=1
    //   filteringAttributes,  → Komma-getrennt, z.B. "name,emailaddress1"
    //   stepName,             → Anzeigename in Dataverse
    //   executionOrder,       → Reihenfolge (1 = default)
    //   isolationMode         → IsolationModeEnum: None=0, Sandbox=1
    // )

    [CrmPluginRegistration(
        "Update",                           // Message
        "account",                          // Entity
        40,                                 // Stage: PostOperation
        1,                                  // Mode: Synchronous
        "name,telephone1,address1_city",    // FilteringAttributes
        "Account: PostUpdate Sync",         // Step Name
        1,                                  // Execution Order
        1,                                  // Isolation: Sandbox
        Image1Type = 0,                     // PreImage
        Image1Name = "PreImage",
        Image1Attributes = "name,telephone1,address1_city"
    )]
    public class AccountPostUpdate : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            var target = (Entity)context.InputParameters["Target"];
            var preImage = context.PreEntityImages["PreImage"];

            // Geschäftslogik hier...
            if (target.Contains("name") && target["name"]?.ToString() != preImage["name"]?.ToString())
            {
                // Name hat sich geändert
            }
        }
    }

    // ─── Async Plugin mit PostImage ────────────────────────────────

    [CrmPluginRegistration(
        "Create",                           // Message
        "contact",                          // Entity
        40,                                 // Stage: PostOperation
        0,                                  // Mode: Asynchronous
        "",                                 // FilteringAttributes (leer = alle)
        "Contact: PostCreate Async",        // Step Name
        1,                                  // Execution Order
        1,                                  // Isolation: Sandbox
        DeleteAsyncOperation = true,        // Async Job nach Erfolg löschen
        Image1Type = 1,                     // PostImage
        Image1Name = "PostImage",
        Image1Attributes = "fullname,emailaddress1,parentcustomerid"
    )]
    public class ContactPostCreate : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // ...
        }
    }

    // ─── PreValidation Step (z.B. für Validierung vor dem Speichern) ─

    [CrmPluginRegistration(
        "Create",                           // Message
        "opportunity",                      // Entity
        10,                                 // Stage: PreValidation
        1,                                  // Mode: Synchronous
        "",                                 // FilteringAttributes
        "Opportunity: PreValidation Create",
        1,
        1
    )]
    public class OpportunityPreValidation : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var target = (Entity)context.InputParameters["Target"];

            // Validierung: Budget muss > 0 sein
            if (target.Contains("budgetamount"))
            {
                var budget = (Money)target["budgetamount"];
                if (budget.Value <= 0)
                    throw new InvalidPluginExecutionException("Budget muss größer als 0 sein.");
            }
        }
    }
}
