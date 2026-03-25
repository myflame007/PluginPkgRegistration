// ═══════════════════════════════════════════════════════════════════════
//  BEISPIEL: Custom API Registration
//
//  So sieht ein Custom API Plugin aus, das unser Tool automatisch
//  in Dataverse registriert — inklusive Request-Parameter und
//  Response-Properties. Kein manuelles Anlegen in der Solution nötig.
//
//  ⚠️ Diese Datei ist NUR ein Beispiel und wird NICHT kompiliert.
//     (Fehlende Referenzen zu Microsoft.Xrm.Sdk, XrmPluginBase etc.)
//
//  Voraussetzung im Plugin-Projekt:
//  Die 3 Attribute + 3 Enums aus CustomApiAttributes.cs müssen im
//  Plugin-Projekt vorhanden sein (Copy-Paste oder NuGet).
//  Unser Tool matched nur auf den Attribut-NAMEN, nicht auf Assembly.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Xrm.Sdk;
using System;
using System.Linq;

namespace MyProject.CustomAPIs
{
    // ─── Beispiel 1: Entity-bound Custom API (Action) ──────────────
    //
    // Szenario: SAP-Antwort verarbeiten und Account aktualisieren.
    // Wird von einem externen System (z.B. Power Automate) aufgerufen.
    //
    // Aufruf via Web API:
    //   POST /api/data/v9.2/ava_transferlogs(guid)/Microsoft.Dynamics.CRM.ava_SapHandleGetAccountResponse
    //   { "ResponseFromSAP": "<EKunnr>12345</EKunnr>...", "DTO": "..." }

    [CrmPluginRegistration("ava_SapHandleGetAccountResponse")]
    [CustomApiDefinition(
        DisplayName = "SAP Handle Get Account Response",
        Description = "Verarbeitet die SAP-Antwort und aktualisiert den Account",
        BindingType = CustomApiBindingType.Entity,
        BoundEntity = "ava_transferlog",
        IsFunction = false,                    // Action (POST), nicht Function (GET)
        IsPrivate = false,                     // Öffentlich aufrufbar
        AllowedProcessingStepType = CustomApiProcessingStepType.SyncAndAsync
    )]
    [CustomApiRequestParameter("ResponseFromSAP", CustomApiParameterType.String, IsRequired = true,
        Description = "XML-Response vom SAP-System")]
    [CustomApiRequestParameter("DTO", CustomApiParameterType.String, IsRequired = false,
        Description = "Optionales Data Transfer Object für Logging")]
    [CustomApiResponseProperty("HasError", CustomApiParameterType.Boolean,
        Description = "True wenn ein Fehler aufgetreten ist")]
    [CustomApiResponseProperty("Message", CustomApiParameterType.String,
        Description = "Status- oder Fehlermeldung")]
    [CustomApiResponseProperty("Accountnumber", CustomApiParameterType.String,
        Description = "SAP-Kundennummer")]
    public class SapHandleGetAccountResponse : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            try
            {
                context.OutputParameters["HasError"] = true;

                var response = context.InputParameters["ResponseFromSAP"]?.ToString()
                    ?? throw new Exception("No response received");

                // Response parsen, Account aktualisieren...
                var accountNumber = ParseAccountNumber(response);

                context.OutputParameters["HasError"] = false;
                context.OutputParameters["Message"] = "Account erfolgreich verarbeitet";
                context.OutputParameters["Accountnumber"] = accountNumber;
            }
            catch (Exception ex)
            {
                context.OutputParameters["HasError"] = true;
                context.OutputParameters["Message"] = ex.Message;
            }
        }

        private static string ParseAccountNumber(string response) => "12345"; // Vereinfacht
    }


    // ─── Beispiel 2: Globale Custom API (Function) ─────────────────
    //
    // Szenario: Systemstatus abfragen (nur lesen, kein Schreiben).
    // IsFunction=true → wird als GET statt POST aufgerufen.
    //
    // Aufruf via Web API:
    //   GET /api/data/v9.2/pub_GetSystemHealthStatus()

    [CrmPluginRegistration("pub_GetSystemHealthStatus")]
    [CustomApiDefinition(
        DisplayName = "Get System Health Status",
        Description = "Gibt den aktuellen Systemstatus zurück",
        BindingType = CustomApiBindingType.Global,
        IsFunction = true,                     // Function (GET) — keine Seiteneffekte
        IsPrivate = false
    )]
    [CustomApiResponseProperty("IsHealthy", CustomApiParameterType.Boolean,
        Description = "True wenn alle Subsysteme erreichbar sind")]
    [CustomApiResponseProperty("StatusMessage", CustomApiParameterType.String,
        Description = "Detaillierter Statusbericht")]
    [CustomApiResponseProperty("LastCheckTimestamp", CustomApiParameterType.DateTime,
        Description = "Zeitpunkt der letzten Prüfung")]
    public class GetSystemHealthStatus : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Keine InputParameters — es ist eine parameterlose Function
            context.OutputParameters["IsHealthy"] = true;
            context.OutputParameters["StatusMessage"] = "Alle Systeme erreichbar";
            context.OutputParameters["LastCheckTimestamp"] = DateTime.UtcNow;
        }
    }


    // ─── Beispiel 3: Custom API mit Entity-Parameter ───────────────
    //
    // Szenario: Einen Contact validieren und anreichern.
    // Nimmt eine EntityReference entgegen und gibt Daten zurück.
    //
    // Aufruf via Web API:
    //   POST /api/data/v9.2/pub_ValidateAndEnrichContact
    //   { "ContactRef": { "@odata.type": "...", "contactid": "..." } }

    [CrmPluginRegistration("pub_ValidateAndEnrichContact")]
    [CustomApiDefinition(
        DisplayName = "Validate and Enrich Contact",
        Description = "Validiert Kontaktdaten und reichert sie mit externen Daten an",
        BindingType = CustomApiBindingType.Global,
        IsFunction = false,
        IsPrivate = true                       // Nur für interne Aufrufe
    )]
    [CustomApiRequestParameter("ContactRef", CustomApiParameterType.EntityReference,
        IsRequired = true, LogicalEntityName = "contact",
        Description = "Referenz auf den zu validierenden Kontakt")]
    [CustomApiRequestParameter("ValidateEmail", CustomApiParameterType.Boolean,
        IsRequired = false, Description = "E-Mail-Adresse extern validieren?")]
    [CustomApiResponseProperty("IsValid", CustomApiParameterType.Boolean,
        Description = "True wenn alle Pflichtfelder befüllt sind")]
    [CustomApiResponseProperty("ValidationErrors", CustomApiParameterType.StringArray,
        Description = "Liste der Validierungsfehler")]
    [CustomApiResponseProperty("EnrichedData", CustomApiParameterType.String,
        Description = "JSON mit angereicherten Daten")]
    public class ValidateAndEnrichContact : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            var contactRef = (EntityReference)context.InputParameters["ContactRef"];
            var validateEmail = context.InputParameters.Contains("ValidateEmail")
                && (bool)context.InputParameters["ValidateEmail"];

            var contact = service.Retrieve("contact", contactRef.Id,
                new Microsoft.Xrm.Sdk.Query.ColumnSet("firstname", "lastname", "emailaddress1"));

            var errors = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(contact.GetAttributeValue<string>("firstname")))
                errors.Add("Vorname fehlt");
            if (string.IsNullOrEmpty(contact.GetAttributeValue<string>("lastname")))
                errors.Add("Nachname fehlt");

            context.OutputParameters["IsValid"] = errors.Count == 0;
            context.OutputParameters["ValidationErrors"] = errors.ToArray();
            context.OutputParameters["EnrichedData"] = "{}";
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  Attribut-Klassen zum Kopieren ins Plugin-Projekt
//
//  Diese Klassen müssen im Plugin-Projekt vorhanden sein, damit der
//  Compiler die Attribute akzeptiert. Unser Tool liest sie per Name
//  via MetadataLoadContext — es ist egal aus welcher Assembly sie kommen.
//
//  → Einfach diese Datei ins Plugin-Projekt kopieren, oder als
//    separates NuGet (Dataverse.PluginRegistration.Attributes) referenzieren.
// ═══════════════════════════════════════════════════════════════════════
//
//  public enum CustomApiBindingType { Global = 0, Entity = 1, EntityCollection = 2 }
//  public enum CustomApiProcessingStepType { None = 0, AsyncOnly = 1, SyncAndAsync = 2 }
//  public enum CustomApiParameterType
//  {
//      Boolean = 0, DateTime = 1, Decimal = 2, Entity = 3, EntityCollection = 4,
//      EntityReference = 5, Float = 6, Integer = 7, Money = 8, Picklist = 9,
//      String = 10, StringArray = 11, Guid = 12
//  }
//
//  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
//  public class CustomApiDefinitionAttribute : Attribute
//  {
//      public string DisplayName { get; set; } = "";
//      public string Description { get; set; } = "";
//      public CustomApiBindingType BindingType { get; set; } = CustomApiBindingType.Global;
//      public string BoundEntity { get; set; } = "";
//      public bool IsFunction { get; set; } = false;
//      public bool IsPrivate { get; set; } = false;
//      public CustomApiProcessingStepType AllowedProcessingStepType { get; set; }
//          = CustomApiProcessingStepType.SyncAndAsync;
//      public string ExecutePrivilegeName { get; set; } = "";
//  }
//
//  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
//  public class CustomApiRequestParameterAttribute : Attribute
//  {
//      public string UniqueName { get; }
//      public CustomApiParameterType Type { get; }
//      public bool IsRequired { get; set; } = true;
//      public string DisplayName { get; set; } = "";
//      public string Description { get; set; } = "";
//      public string LogicalEntityName { get; set; } = "";
//      public CustomApiRequestParameterAttribute(string uniqueName, CustomApiParameterType type)
//      { UniqueName = uniqueName; Type = type; }
//  }
//
//  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
//  public class CustomApiResponsePropertyAttribute : Attribute
//  {
//      public string UniqueName { get; }
//      public CustomApiParameterType Type { get; }
//      public string DisplayName { get; set; } = "";
//      public string Description { get; set; } = "";
//      public string LogicalEntityName { get; set; } = "";
//      public CustomApiResponsePropertyAttribute(string uniqueName, CustomApiParameterType type)
//      { UniqueName = uniqueName; Type = type; }
//  }
