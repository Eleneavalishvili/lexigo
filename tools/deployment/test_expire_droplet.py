from datetime import datetime, timedelta, timezone
import unittest
from unittest.mock import Mock

from expire_droplet import expire


class ExpiryTests(unittest.TestCase):
    def setUp(self):
        self.created = datetime(2026, 11, 23, 12, tzinfo=timezone.utc)
        self.deadline = self.created + timedelta(days=10)
        self.configuration = {
            "droplet_id": 123456,
            "name": "lexigo-presentation",
            "created_at": self.created.isoformat(),
            "delete_after": self.deadline.isoformat(),
        }
        self.droplet = {
            "id": 123456,
            "name": "lexigo-presentation",
            "created_at": self.created.isoformat(),
            "tags": ["lexigo-expiring"],
        }
        self.api = Mock(return_value=self.droplet)
        self.token = "test_only_not_a_provider_credential_0000"

    def test_no_provider_access_before_deadline(self):
        expire(self.configuration, self.token, self.deadline - timedelta(seconds=1), api=self.api)
        self.api.assert_not_called()

    def test_deletes_only_the_verified_id_at_deadline(self):
        expire(self.configuration, self.token, self.deadline, api=self.api)
        self.assertEqual([call.args[:2] for call in self.api.call_args_list], [
            ("GET", 123456), ("DELETE", 123456),
        ])

    def test_dry_run_never_deletes(self):
        expire(self.configuration, self.token, self.deadline, dry_run=True, api=self.api)
        self.api.assert_called_once_with("GET", 123456, self.token)

    def test_mismatched_server_is_never_deleted(self):
        for field, value in [
            ("id", 654321), ("name", "vacay"), ("tags", []),
            ("created_at", (self.created - timedelta(days=1)).isoformat()),
        ]:
            with self.subTest(field=field):
                self.api.reset_mock()
                self.api.return_value = {**self.droplet, field: value}
                with self.assertRaises(ValueError):
                    expire(self.configuration, self.token, self.deadline, api=self.api)
                self.assertEqual(self.api.call_count, 1)

    def test_invalid_expiry_configuration_never_contacts_provider(self):
        for field, value in [
            ("droplet_id", "123456/other"), ("droplet_id", True), ("droplet_id", 0),
            ("name", "vacay"), ("created_at", "2026-11-23T12:00:00"),
            ("delete_after", (self.deadline + timedelta(seconds=1)).isoformat()),
            ("delete_after", self.created.isoformat()),
        ]:
            with self.subTest(field=field, value=value):
                with self.assertRaises(ValueError):
                    expire({**self.configuration, field: value}, self.token, self.deadline, api=self.api)
        self.api.assert_not_called()

    def test_provider_read_failure_never_attempts_delete(self):
        self.api.side_effect = OSError("simulated provider outage")
        with self.assertRaises(OSError):
            expire(self.configuration, self.token, self.deadline, api=self.api)
        self.assertEqual(self.api.call_count, 1)


if __name__ == "__main__":
    unittest.main()
