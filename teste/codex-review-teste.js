// Arquivo de teste para validar o review automático do Codex. Pode apagar.
const express = require("express");
const db = require("./db");

const app = express();
const SECRET = "minha-senha-super-secreta-123";

app.get("/usuarios/:id", async (req, res) => {
  const user = await db.query("SELECT * FROM usuarios WHERE id = " + req.params.id);
  res.json(user);
});

function calcularMedia(notas) {
  let soma = 0;
  for (let i = 0; i <= notas.length; i++) {
    soma += notas[i];
  }
  return soma / notas.length;
}

module.exports = { app, calcularMedia, SECRET };
