import unittest
from datetime import date

from tsc_crawler import normalize as n

TODAY = date(2026, 10, 13)


class SpeciesTests(unittest.TestCase):
    def test_keywords(self):
        self.assertEqual(n.normalize_species("Labrador Rüde"), "Hund")
        self.assertEqual(n.normalize_species("Europäisch Kurzhaar Kater"), "Katze")
        self.assertEqual(n.normalize_species("Zwergkaninchen"), "Kleintier")
        self.assertEqual(n.normalize_species("Wellensittich"), "Vogel")
        self.assertEqual(n.normalize_species("Shetlandpony"), "Pferd")
        self.assertEqual(n.normalize_species("Landschildkröte"), "Reptil")

    def test_unknown_uses_default_or_sonstige(self):
        self.assertEqual(n.normalize_species("Alpaka"), "Sonstige")
        self.assertEqual(n.normalize_species(None, "Hund"), "Hund")

    def test_earliest_keyword_wins(self):
        self.assertEqual(n.normalize_species("Hund (verträgt Katzen)"), "Hund")


class SexTests(unittest.TestCase):
    def test_values(self):
        self.assertEqual(n.normalize_sex("weiblich"), "Weiblich")
        self.assertEqual(n.normalize_sex("Hündin"), "Weiblich")
        self.assertEqual(n.normalize_sex("männlich"), "Maennlich")
        self.assertEqual(n.normalize_sex("Rüde"), "Maennlich")
        self.assertEqual(n.normalize_sex("m"), "Maennlich")
        self.assertEqual(n.normalize_sex("w"), "Weiblich")
        self.assertEqual(n.normalize_sex("?"), "Unbekannt")
        self.assertEqual(n.normalize_sex(None), "Unbekannt")


class SizeAndCompatTests(unittest.TestCase):
    def test_size(self):
        self.assertEqual(n.normalize_size("groß"), "Gross")
        self.assertEqual(n.normalize_size("Gross"), "Gross")
        self.assertEqual(n.normalize_size("klein bis mittel"), "Klein")
        self.assertEqual(n.normalize_size("k.A."), "Unbekannt")

    def test_compat_negation_first(self):
        self.assertEqual(n.normalize_compat("verträglich"), "Ja")
        self.assertEqual(n.normalize_compat("nicht verträglich"), "Nein")
        self.assertEqual(n.normalize_compat("unverträglich"), "Nein")
        self.assertEqual(n.normalize_compat("ja"), "Ja")
        self.assertEqual(n.normalize_compat("nein"), "Nein")
        self.assertEqual(n.normalize_compat("weiß man nicht"), "Unbekannt")
        self.assertEqual(n.normalize_compat(None), "Unbekannt")
        self.assertEqual(n.normalize_compat("keine Katzen"), "Nein")

    def test_slugify(self):
        self.assertEqual(n.slugify("Käthe & Bärli!"), "kaethe-baerli")
        self.assertEqual(n.slugify("***"), "tier")

    def test_neutered(self):
        self.assertIs(n.normalize_neutered("kastriert"), True)
        self.assertIs(n.normalize_neutered("nicht kastriert"), False)
        self.assertIs(n.normalize_neutered("unkastriert"), False)
        self.assertIsNone(n.normalize_neutered("k.A."))


class NumberTests(unittest.TestCase):
    def test_fee(self):
        self.assertEqual(n.parse_fee("250 €"), 250.0)
        self.assertEqual(n.parse_fee("250,50 EUR"), 250.5)
        self.assertEqual(n.parse_fee("1.200 €"), 1200.0)
        self.assertEqual(n.parse_fee("1.200,00 €"), 1200.0)
        self.assertIsNone(n.parse_fee("auf Anfrage"))

    def test_weight(self):
        self.assertEqual(n.parse_weight_kg("28,5 kg"), 28.5)
        self.assertEqual(n.parse_weight_kg("ca. 4 KG"), 4.0)
        self.assertIsNone(n.parse_weight_kg("schwer"))


class AgeTests(unittest.TestCase):
    def test_relative_ages(self):
        self.assertEqual(n.parse_age("3 Jahre", TODAY), (36, None))
        self.assertEqual(n.parse_age("2 Jahre 4 Monate", TODAY), (28, None))
        self.assertEqual(n.parse_age("ca. 8 Monate", TODAY), (8, None))
        self.assertEqual(n.parse_age("12 Wochen", TODAY), (3, None))
        self.assertEqual(n.parse_age("1,5 Jahre", TODAY), (18, None))

    def test_birth_date(self):
        months, born = n.parse_age("geb. 05.03.2021", TODAY)
        self.assertEqual(born, date(2021, 3, 5))
        self.assertEqual(months, 67)

    def test_birth_month_only(self):
        months, born = n.parse_age("geb. 03/2021", TODAY)
        self.assertEqual(born, date(2021, 3, 1))
        self.assertEqual(months, 67)

    def test_unparseable(self):
        self.assertEqual(n.parse_age("jung", TODAY), (None, None))
        self.assertEqual(n.parse_age(None, TODAY), (None, None))

    def test_birth_date_in_future_is_not_negative(self):
        months, _ = n.parse_age("01.12.2026", TODAY)
        self.assertEqual(months, 0)


if __name__ == "__main__":
    unittest.main()
