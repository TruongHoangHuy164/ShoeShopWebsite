document.addEventListener("DOMContentLoaded", function () {
    const container = document.querySelector(".container");
    const loginBtn = document.querySelector("#login-toggle");
    const registerBtn = document.querySelector("#register-toggle");

    // Kiểm tra xem các phần tử có tồn tại không
    if (!container) {
        console.error("Container không được tìm thấy. Kiểm tra class 'container' trong HTML.");
        return;
    }
    if (!loginBtn) {
        console.error("Nút Đăng nhập không được tìm thấy. Kiểm tra ID 'login-toggle' trong HTML.");
        return;
    }
    if (!registerBtn) {
        console.error("Nút Đăng ký không được tìm thấy. Kiểm tra ID 'register-toggle' trong HTML.");
        return;
    }

    // Kiểm tra xem trang có form đăng nhập không (trang Login.cshtml)
    const hasLoginForm = container.querySelector(".form-box.login") !== null;

    if (hasLoginForm) {
        // Logic cho trang Login.cshtml
        loginBtn.addEventListener("click", () => {
            // Không làm gì vì đã ở trang đăng nhập
            console.log("Đã ở trang đăng nhập");
        });

        registerBtn.addEventListener("click", () => {
            const href = registerBtn.getAttribute("href");
            if (href) {
                window.location.href = href;
                console.log("Điều hướng đến trang đăng ký: " + href);
            } else {
                console.error("Không tìm thấy thuộc tính href trên nút Đăng Ký. Kiểm tra asp-page.");
            }
        });
    } else {
        // Logic cho trang Register.cshtml
        loginBtn.addEventListener("click", () => {
            const href = loginBtn.getAttribute("href");
            if (href) {
                window.location.href = href;
                console.log("Điều hướng đến trang đăng nhập: " + href);
            } else {
                console.error("Không tìm thấy thuộc tính href trên nút Đăng Nhập. Kiểm tra asp-page.");
            }
        });

        registerBtn.addEventListener("click", () => {
            // Không làm gì vì đã ở trang đăng ký
            console.log("Đã ở trang đăng ký");
        });
    }
});