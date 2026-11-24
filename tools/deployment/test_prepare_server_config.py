import unittest

from prepare_server_config import validate_input


class ConfigurationInputTests(unittest.TestCase):
    def setUp(self):
        self.settings = {
            "public_ip": "1.1.1.1",
            "database_image": "lexigo-postgres:patched",
            "database_uid": 70,
            "api_image": "lexigo-api:deployment",
            "smtp": {
                "Username": "fixture-user", "Password": "fixture-password",
                "SenderAddress": "support@example.test",
            },
        }

    def test_public_origin_and_known_non_root_database_user_are_accepted(self):
        address, _, database_uid = validate_input(self.settings)
        self.assertEqual((address, database_uid), ("1.1.1.1", 70))

    def test_private_origins_and_address_injection_are_rejected(self):
        for address in ("127.0.0.1", "10.0.0.1", "169.254.169.254", "::1", "1.1.1.1;other"):
            with self.subTest(address=address), self.assertRaises(ValueError):
                validate_input({**self.settings, "public_ip": address})

    def test_unpinned_database_and_unverified_root_identity_are_rejected(self):
        for key, value in (("database_image", "postgres:17"), ("database_uid", 0),
                           ("database_uid", True), ("api_image", "unreviewed:latest")):
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                validate_input({**self.settings, key: value})

    def test_missing_delivery_credentials_are_rejected(self):
        for field in ("Username", "Password", "SenderAddress"):
            smtp = {**self.settings["smtp"], field: ""}
            with self.subTest(field=field), self.assertRaises(ValueError):
                validate_input({**self.settings, "smtp": smtp})


if __name__ == "__main__":
    unittest.main()
