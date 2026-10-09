SELECT octet_length(@snapshot::jsonb::text)<=@maximum
   AND octet_length(@source::jsonb::text)<=@maximum
   AND octet_length(@event::jsonb::text)<=@maximum
   AND octet_length(@receipt::jsonb::text)<=@maximum;
