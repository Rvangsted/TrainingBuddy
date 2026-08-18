namespace TrainingBuddy.FireBase
{
	/// <summary>
	/// Every Danish, user-safe string shown by the error-messaging helper — see
	/// ErrorMessaging_Scope.md. Kept in one dedicated file, separate from business logic, so
	/// copy can be reviewed and tweaked without hunting through DatabaseManager/FirebaseController.
	/// These are also the literal messages of the app's deliberately-thrown InvalidOperationExceptions
	/// (DatabaseManager.ShowError shows an InvalidOperationException's message as-is, on the
	/// assumption that it's always one of these constants — never a raw/technical/English string).
	/// </summary>
	public static class UserMessages
	{
		public const string GenericFallback = "Der opstod en fejl. Prøv venligst igen.";

		// Shared across every "you must be logged in" / "your account is deactivated" guard —
		// same underlying condition regardless of which action triggered it.
		public const string NotAuthenticated   = "Du skal være logget ind for at gøre dette.";
		public const string AccountDeactivated = "Din konto er deaktiveret.";

		// Race lifecycle
		public const string RaceNotFound             = "Løbet blev ikke fundet.";
		public const string RaceNotOpen              = "Løbet er ikke åbent for tilmelding længere.";
		public const string RaceFull                 = "Løbet er fyldt.";
		public const string OnlyHostCanManageRequests = "Kun værten eller en admin kan håndtere anmodninger om at deltage.";
		public const string OnlyHostCanKick           = "Kun værten eller en admin kan smide deltagere ud.";
		public const string OnlyHostCanStartRace      = "Kun værten eller en admin kan starte løbet.";
		public const string OnlyHostCanCancelRace     = "Kun værten eller en admin kan aflyse løbet.";
		public const string HostCannotKickSelf        = "Værten kan ikke smide sig selv ud.";
		public const string CannotLeaveActiveRace     = "Du kan ikke forlade et løb, der er i gang eller afsluttet.";
		public const string HostMustCancelNotLeave    = "Som vært skal du aflyse løbet i stedet for at forlade det.";
		public const string JoinRequestNotApproved    = "Din anmodning om at deltage er endnu ikke godkendt.";
		public const string CannotCancelCompletedRace = "Et afsluttet løb kan ikke aflyses.";
		public const string AlreadyHostingRace           = "Du er allerede vært for et aktivt løb.";
		public const string CannotHostWhileParticipating = "Du kan ikke oprette et løb, mens du deltager i et andet aktivt løb.";
		public const string CannotJoinWhileHosting       = "Du kan ikke deltage i et andet løb, mens du er vært for et aktivt løb.";
		public const string AlreadyInRace                = "Du deltager allerede i et aktivt løb.";

		public static string NotEnoughParticipants(int minRequired) =>
			$"Løbet kræver mindst {minRequired} spillere for at starte.";

		public static string InsufficientStepCurrency(int cost, long balance) =>
			$"Du har ikke nok mønter til dette niveau. Det koster {cost}, og du har {balance}.";

		// Friends
		public const string CannotFriendSelf = "Du kan ikke sende en venneanmodning til dig selv.";

		// Login (FirebaseController.FirebaseLogin)
		public const string LoginMissingEmail    = "Indtast venligst din emailadresse.";
		public const string LoginMissingPassword = "Indtast venligst din adgangskode.";
		public const string LoginWrongPassword   = "Forkert adgangskode.";
		public const string LoginInvalidEmail    = "Emailadressen ser ugyldig ud.";
		public const string LoginNoSuchAccount   = "Der findes ingen konto med denne email.";
		public const string LoginFailed          = "Login mislykkedes. Prøv venligst igen.";

		// Register (FirebaseController.FirebaseRegister) — the pre-submit field-validation
		// guards logged and returned false with no UI feedback at all until this pass; a genuine
		// gap, not something the original error-messaging retrofit list covered.
		public const string RegisterTitle                 = "Registrering fejlede";
		public const string RegisterMissingUsername        = "Indtast venligst et brugernavn.";
		public const string RegisterMissingSex              = "Vælg venligst dit køn.";
		public const string RegisterMissingEmail            = "Indtast venligst din emailadresse.";
		public const string RegisterMissingPassword         = "Indtast venligst en adgangskode.";
		public const string RegisterMissingPasswordConfirm  = "Bekræft venligst din adgangskode.";
		public const string RegisterPasswordMismatch        = "Adgangskoderne er ikke ens.";
		public const string RegisterMissingDateOfBirth      = "Udfyld venligst hele din fødselsdato.";
		public const string RegisterEmailTaken              = "En konto med denne email findes allerede. Log ind i stedet.";
		public const string RegisterFailed                  = "Registrering fejlede. Tjek venligst at oplysningerne er korrekte.";

		// Delete account (DatabaseManager.DeleteAccountAsync)
		public const string DeleteAccountTitle             = "Slet konto";
		public const string DeleteAccountWrongPassword     = "Forkert adgangskode. Din konto blev ikke slettet.";
		public const string DeleteAccountCleanupFailed      = "Der opstod en fejl under sletning af dine data. Prøv venligst igen.";
		public const string DeleteAccountAuthDeletionFailed = "Dine data blev fjernet, men login-oplysningerne kunne ikke slettes. Kontakt venligst support.";
	}
}
