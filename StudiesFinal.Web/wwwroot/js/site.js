// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
    document.addEventListener('DOMContentLoaded', function() {
            var backToTopBtn = document.getElementById('backToTopBtn');

    if (backToTopBtn) {
        window.addEventListener('scroll', function () {
            if (window.scrollY > 300) {
                backToTopBtn.classList.add('visible');
            } else {
                backToTopBtn.classList.remove('visible');
            }
        });

    backToTopBtn.addEventListener('click', function(e) {
        e.preventDefault();
    window.scrollTo({top: 0, behavior: 'smooth' });
                });
            }
        });
