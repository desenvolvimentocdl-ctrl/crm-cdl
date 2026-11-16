-- Cria o banco do CRM no MESMO servidor MySQL do site, sem tocar no banco do site.
-- Rode uma única vez, com um usuário administrador do MySQL.
-- Troque a senha abaixo por uma senha forte antes de executar.

CREATE DATABASE IF NOT EXISTS CrmCDL
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

-- Usuário próprio do CRM: só enxerga o banco CrmCDL.
CREATE USER IF NOT EXISTS 'crmcdl'@'%' IDENTIFIED BY 'TROQUE_ESTA_SENHA';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, DROP, REFERENCES ON CrmCDL.* TO 'crmcdl'@'%';
FLUSH PRIVILEGES;

-- Conferência: deve listar apenas o banco CrmCDL.
-- SHOW GRANTS FOR 'crmcdl'@'%';
