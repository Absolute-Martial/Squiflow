SELECT octet_length(@debtor::jsonb::text)<=8192 AND octet_length(@receipt::jsonb::text)<=4096;
