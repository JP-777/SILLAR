-- ============================================================================
-- SILLAR · Módulo CORE · Datos semilla
--
-- Requiere que las migraciones de EF Core del módulo ya estén aplicadas
-- (ADR-009: las tablas las crean las migraciones, no este script).
--
--   dotnet ef database update --project backend/Sillar.Core \
--                             --startup-project backend/Sillar.Api
--
-- Idempotente: ejecutarlo dos veces no duplica filas ni falla.
-- Nunca sobrescribe valores ya configurados por el negocio: usa DO NOTHING.
--
-- Este script NO crea usuarios administradores. El primer super_admin se crea
-- en el modo instalación, con una contraseña que elige la persona (SPEC §4.11).
-- ============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- Configuración general del sitio
--
-- PENDIENTE_DEFINIR marca los valores que completa cada instalación. Este
-- repositorio contiene el producto, nunca los datos de un negocio real.
--
-- Todas las claves de abajo son públicas: se sirven en /api/settings/public y
-- alimentan la cabecera, el pie y la página de contacto. is_public vale false
-- por defecto en la tabla; publicar algo es siempre un acto deliberado.
-- ----------------------------------------------------------------------------
INSERT INTO core.site_settings (setting_key, setting_value, value_type, description, is_public, is_active)
VALUES
    ('business_name',      'PENDIENTE_DEFINIR', 'text',  'Nombre comercial del negocio',                        true, true),
    ('main_message',       'PENDIENTE_DEFINIR', 'text',  'Mensaje principal de la página de inicio',            true, true),
    ('whatsapp_number',    'PENDIENTE_DEFINIR', 'text',  'Número de WhatsApp de atención, con código de país',  true, true),
    ('contact_email',      'PENDIENTE_DEFINIR', 'email', 'Correo de contacto público',                          true, true),
    ('contact_phone',      'PENDIENTE_DEFINIR', 'text',  'Teléfono de contacto público',                        true, true),
    ('business_address',   'PENDIENTE_DEFINIR', 'text',  'Dirección del local',                                 true, true),
    ('business_reference', 'PENDIENTE_DEFINIR', 'text',  'Referencia para ubicar el local',                     true, true),
    ('google_maps_url',    'PENDIENTE_DEFINIR', 'url',   'Enlace al local en Google Maps',                      true, true),
    ('business_hours',     'PENDIENTE_DEFINIR', 'text',  'Horario de atención',                                 true, true),
    ('currency_code',      'PEN',               'text',  'Código ISO de la moneda',                             true, true),
    ('currency_symbol',    'S/',                'text',  'Símbolo de la moneda, para mostrar precios',          true, true)
ON CONFLICT (setting_key) DO NOTHING;

-- ----------------------------------------------------------------------------
-- Correo saliente
--
-- Nunca públicas. La contraseña NO vive en site_settings: se inyecta mediante
-- SILLAR_SMTP_PASSWORD. El puerto 587 es un valor inicial razonable; host y
-- remitente deben configurarse para cada instalación.
-- ----------------------------------------------------------------------------
INSERT INTO core.site_settings (setting_key, setting_value, value_type, description, is_public, is_active)
VALUES
    ('smtp_server', 'PENDIENTE_DEFINIR', 'text',   'Servidor SMTP de correo saliente', false, true),
    ('smtp_port',   '587',               'number', 'Puerto del servidor SMTP',          false, true),
    ('smtp_from',   'PENDIENTE_DEFINIR', 'email',  'Correo remitente y usuario SMTP',   false, true)
ON CONFLICT (setting_key) DO NOTHING;

UPDATE core.site_settings
SET is_public = false
WHERE setting_key IN ('smtp_server', 'smtp_port', 'smtp_from');

COMMIT;

-- ----------------------------------------------------------------------------
-- Verificación rápida
--
--   SELECT setting_key, setting_value, is_public FROM core.site_settings
--   ORDER BY setting_key;
--
-- Deben aparecer 14 filas: 11 públicas y las 3 smtp_* privadas.
-- ----------------------------------------------------------------------------
