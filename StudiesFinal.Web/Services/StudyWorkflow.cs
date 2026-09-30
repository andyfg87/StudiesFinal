using StudiesFinal.Models.Entities;
using StudiesFinal.Web.Utils;
using System.Security.Claims;

namespace StudiesFinal.Web.Services
{
    /// <summary>
    /// Reglas del ciclo de vida de un estudio:
    ///   En progreso -> Por firmar -> Completado (firmado)
    ///
    /// - Los estudios nuevos solo se crean en "En progreso".
    /// - Técnicos: crean y editan en "En progreso" y los envían a firmar.
    /// - Doctor: lo ve todo, edita "En progreso" y "Por firmar", firma, devuelve
    ///   a progreso y es el único que puede desbloquear un estudio firmado.
    /// - Admin: gestiona usuarios y plantillas; edita como el doctor pero no firma
    ///   ni desbloquea.
    /// - Un estudio firmado no se puede editar hasta que el doctor lo desbloquee
    ///   (vuelve a "Por firmar" y pierde la firma).
    /// </summary>
    public static class StudyWorkflow
    {
        public static bool CanEdit(Study study, ClaimsPrincipal user) => study.Status switch
        {
            StudyStatus.InProgress => true,
            StudyStatus.ToSign => user.IsInRole(Roles.Doctor) || user.IsInRole(Roles.Admin),
            _ => false // Completado: bloqueado
        };

        public static bool CanSendToSign(Study study, ClaimsPrincipal user)
            => study.Status == StudyStatus.InProgress;

        public static bool CanReturnToProgress(Study study, ClaimsPrincipal user)
            => study.Status == StudyStatus.ToSign && (user.IsInRole(Roles.Doctor) || user.IsInRole(Roles.Admin));

        public static bool CanSign(Study study, ClaimsPrincipal user)
            => study.Status == StudyStatus.ToSign && user.IsInRole(Roles.Doctor);

        public static bool CanUnlock(Study study, ClaimsPrincipal user)
            => study.Status == StudyStatus.Completed && user.IsInRole(Roles.Doctor);

        public static bool CanDelete(Study study, ClaimsPrincipal user)
            => study.Status != StudyStatus.Completed && user.IsInRole(Roles.Admin);

        public static bool CanManageTemplates(ClaimsPrincipal user)
            => user.IsInRole(Roles.Doctor) || user.IsInRole(Roles.Admin);
    }
}
