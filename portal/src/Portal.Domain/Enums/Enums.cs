namespace TierschutzCollect.Domain.Enums;

public enum OrganizationType { Tierheim, Verein, Pflegestelle, Sonstige }

public enum Species { Hund, Katze, Kleintier, Vogel, Pferd, Reptil, Sonstige }

public enum Sex { Unbekannt, Maennlich, Weiblich }

public enum AnimalSize { Unbekannt, Klein, Mittel, Gross }

public enum AnimalStatus { Vermittelbar, Reserviert, Vermittelt, Archiviert }

/// <summary>Yes/No/Unknown – important because many sources simply do not provide this information.</summary>
public enum Compatibility { Unbekannt, Ja, Nein }

public enum DataSource { Manuell, Crawler, Plugin, Feed }
