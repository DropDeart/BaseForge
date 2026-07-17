using System.Runtime.CompilerServices;

// Entegrasyon testlerinin gerçek (internal) EventEnvelope'u kullanabilmesi için — testler aksi
// halde aynı JSON şeklini elle kopyalayan bir "test-only" record'a muhtaç kalırdı.
[assembly: InternalsVisibleTo("BaseForge.IntegrationTests")]
