document.addEventListener("DOMContentLoaded", function () {
  const form = document.getElementById("loginForm");

  form.addEventListener("submit", async function (e) {
      e.preventDefault();

      const username = document.getElementById("username").value;
      const password = document.getElementById("password").value;

      const response = await fetch("/Login/Login", {
          method: "POST",
          headers: {
              "Content-Type": "application/json"
          },
          body: JSON.stringify({ username, password })
      });

      const data = await response.json();

      if (response.ok) {
          alert("✅ " + data.message);
          window.location.href = "/Home/Index";
      } else {
          alert("❌ " + data.message);
      }
  });
});
