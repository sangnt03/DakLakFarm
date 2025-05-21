// wwwroot/js/farmer-product.js
(function () {
    console.log('🐶 farmer-product.js loaded');

    var categories = [];

    // Load danh mục
    async function loadCategories() {
        try {
            const res = await fetch('/api/farmer/categories');
            if (!res.ok) {
                console.error('Lỗi khi tải danh mục:', res.status, await res.text());
                return;
            }
            categories = await res.json();
        } catch (error) {
            console.error('Lỗi mạng khi tải danh mục:', error);
        }
    }

    // Đổ danh mục vào select
    function populateCategorySelect() {
        const sel = document.getElementById('categoryId');
        if (!sel) return;
        sel.innerHTML = '<option value="">-- Chọn danh mục --</option>';
        categories.forEach(c => {
            const opt = document.createElement('option');
            opt.value = c.categoryId;
            opt.textContent = c.categoryName;
            sel.appendChild(opt);
        });
    }

    // Load & render sản phẩm
    async function loadProducts() {
        try {
            const res = await fetch('/api/farmer/products');
            if (!res.ok) {
                console.error('Lỗi khi tải sản phẩm:', res.status, await res.text());
                return;
            }
            const list = await res.json();
            const tbody = document.getElementById('productTableBody');
            if (!tbody) return;
            tbody.innerHTML = '';
            list.forEach(p => {
                const raw = p.imageUrls?.[0] || '/images/no-image.png';
                const imgUrl = raw.startsWith('/') ? raw : '/' + raw;
                const tr = document.createElement('tr');
                tr.innerHTML = `
          <td><img src="${imgUrl}"
                   style="width:60px;height:60px;object-fit:cover;"
                   onerror="this.src='/images/no-image.png'" /></td>
          <td>${p.productName}</td>
          <td>${p.categoryName}</td>
          <td>${p.price.toLocaleString()}</td>
          <td>${p.quantityAvailable}</td>
          <td>${p.unit || ''}</td>
          <td>${p.description || ''}</td>
          <td>${new Date(p.createdAt).toLocaleDateString()}</td>
          <td>
            <button class="btn btn-sm btn-warning"
                    onclick="showForm('edit', ${p.productId})">Sửa</button>
            <button class="btn btn-sm btn-danger"
                    onclick="del(${p.productId})">Xóa</button>
          </td>`;
                tbody.appendChild(tr);
            });
        } catch (error) {
            console.error('Lỗi mạng khi tải sản phẩm:', error);
        }
    }

    // Xóa sản phẩm
    async function del(id) {
        if (!confirm('Xác nhận xóa?')) return;
        try {
            const res = await fetch(`/api/farmer/products/${id}`, { method: 'DELETE' });
            if (!res.ok) {
                console.error('Lỗi khi xóa sản phẩm:', res.status, await res.text());
                alert('Xóa sản phẩm thất bại!');
                return;
            }
            loadProducts();
        } catch (error) {
            console.error('Lỗi mạng khi xóa:', error);
            alert('Có lỗi xảy ra, vui lòng thử lại sau!');
        }
    }

    // Show form Create/Edit
    async function showForm(mode, id) {
        const modalEl = document.getElementById('productModal');
        if (!modalEl) return;
        const modal = new bootstrap.Modal(modalEl);
        const form = modalEl.querySelector('#productForm');
        if (!form) return;

        form.reset();
        form.dataset.mode = mode;
        if (mode === 'edit') form.dataset.id = id;
        else delete form.dataset.id;

        populateCategorySelect();

        const titleEl = modalEl.querySelector('#productModalLabel');
        if (titleEl) {
            titleEl.textContent = mode === 'create' ? 'Thêm sản phẩm' : 'Sửa sản phẩm';
        }

        if (mode === 'edit') {
            try {
                const res = await fetch(`/api/farmer/products/${id}`);
                if (!res.ok) {
                    console.error('Lỗi khi tải sản phẩm:', res.status, await res.text());
                    alert('Không thể tải dữ liệu sản phẩm!');
                    return;
                }
                const product = await res.json();

                // Gán giá trị cho form qua id trong modal
                const mapping = {
                    productName: product.productName,
                    categoryId: product.categoryId,
                    price: product.price,
                    quantityAvailable: product.quantityAvailable,
                    unit: product.unit,
                    description: product.description
                };
                for (const [fieldId, value] of Object.entries(mapping)) {
                    const input = modalEl.querySelector(`#${fieldId}`);
                    if (input) input.value = value ?? '';
                }

                // Hiển thị ảnh cũ
                const currentImagesDiv = modalEl.querySelector('#currentImages');
                if (currentImagesDiv) {
                    currentImagesDiv.innerHTML = '';
                    if (Array.isArray(product.imageUrls) && product.imageUrls.length) {
                        product.imageUrls.forEach(url => {
                            const img = document.createElement('img');
                            img.src = url.startsWith('/') ? url : '/' + url;
                            img.style = 'width:100px;height:100px;object-fit:cover;';
                            img.className = 'me-2 mb-2';
                            img.onerror = () => img.src = '/images/no-image.png';
                            currentImagesDiv.appendChild(img);
                        });
                    } else {
                        currentImagesDiv.innerHTML = '<p>Không có ảnh</p>';
                    }
                }
            } catch (error) {
                console.error('Lỗi mạng khi tải sản phẩm:', error);
                alert('Có lỗi xảy ra, vui lòng thử lại sau!');
            }
        }

        modal.show();
    }

    // Xử lý submit form Create/Edit
    async function onSubmit(e) {
        e.preventDefault();
        const form = e.target;
        const mode = form.dataset.mode;
        const id = form.dataset.id;
        const fd = new FormData(form);

        // Helper lấy giá trị từ input id
        const getVal = sel => {
            const el = document.getElementById(sel);
            return el ? el.value.trim() : '';
        };

        // Client-side validation
        if (!getVal('categoryId')) {
            return alert('Vui lòng chọn danh mục!');
        }
        if (!getVal('productName')) {
            return alert('Vui lòng nhập tên sản phẩm!');
        }
        const price = parseFloat(getVal('price'));
        if (isNaN(price) || price <= 0) {
            return alert('Vui lòng nhập giá hợp lệ (số lớn hơn 0)!');
        }
        const qty = parseInt(getVal('quantityAvailable'), 10);
        if (isNaN(qty) || qty < 0) {
            return alert('Vui lòng nhập số lượng hợp lệ (số không âm)!');
        }
        const desc = getVal('description');
        if (desc.length > 500) {
            return alert('Mô tả không được vượt quá 500 ký tự!');
        }

        if (mode === 'create') {
            fd.delete('productId');
        }

        // Append file mới (nếu có)
        const files = document.getElementById('productImages')?.files;
        if (files) {
            Array.from(files).forEach(f => fd.append('ProductImages', f));
        }

        const url = mode === 'create'
            ? '/api/farmer/products'
            : `/api/farmer/products/${id}`;
        const method = mode === 'create' ? 'POST' : 'PUT';

        try {
            const res = await fetch(url, { method, body: fd });
            if (!res.ok) {
                let err = `Lỗi ${res.status}`;
                const ct = res.headers.get('content-type');
                if (ct?.includes('application/json')) {
                    const js = await res.json();
                    err = js.error || js.message || err;
                } else {
                    err = await res.text();
                }
                console.error('Lỗi lưu sản phẩm:', err);
                alert(`Lỗi: ${err}`);
                return;
            }

            bootstrap.Modal.getInstance(
                document.getElementById('productModal')
            )?.hide();
            await loadProducts();
            alert('Lưu sản phẩm thành công!');
        } catch (error) {
            console.error('Lỗi mạng:', error);
            alert('Có lỗi xảy ra, vui lòng thử lại sau!');
        }
    }

    // Khởi chạy khi DOM đã sẵn sàng
    document.addEventListener('DOMContentLoaded', async () => {
        await loadCategories();
        populateCategorySelect();
        await loadProducts();

        document.getElementById('btnCreate')?.addEventListener('click', () => showForm('create'));
        document.getElementById('productForm')?.addEventListener('submit', onSubmit);
    });

})();
